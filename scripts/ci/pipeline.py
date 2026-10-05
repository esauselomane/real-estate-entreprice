#!/usr/bin/env python3
"""Select, check and release independently deployable components."""
import argparse
import fnmatch
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def run(args, cwd=ROOT, capture=False):
    return subprocess.run(args, cwd=cwd, check=True, text=True,
                          stdout=subprocess.PIPE if capture else None).stdout


def configuration():
    return json.loads((ROOT / "scripts/ci/services.json").read_text())


def project_inputs(project, visited=None):
    visited = set() if visited is None else visited
    path = (ROOT / project).resolve()
    if path in visited or not path.exists():
        return set()
    visited.add(path)
    folders = {str(path.parent.relative_to(ROOT)) + "/"}
    for reference in ET.parse(path).iter("ProjectReference"):
        include = reference.get("Include", "").replace("\\", "/")
        child = (path.parent / include).resolve().relative_to(ROOT)
        folders |= project_inputs(str(child), visited)
    return folders


def affected_services(files, config):
    selected = set()
    for name, service in config["services"].items():
        folders = set(service.get("folders", []))
        if service.get("project"):
            folders |= project_inputs(service["project"])
        patterns = service.get("files", []) + [service["dockerfile"]]
        for file in files:
            if file.endswith(("appsettings.Development.json", "appsettings.local.json")):
                continue
            if any(file.startswith(folder) for folder in folders) or any(
                fnmatch.fnmatchcase(file, pattern) for pattern in patterns
            ):
                selected.add(name)
    return sorted(selected)


def github_baseline():
    """Use the last successful main push so failed deployments are not forgotten."""
    token = os.environ.get("GH_TOKEN")
    repository = os.environ.get("GITHUB_REPOSITORY")
    if not token or not repository:
        return None
    url = (f"https://api.github.com/repos/{repository}/actions/workflows/ci-cd.yml/runs"
           "?branch=main&event=push&status=success&per_page=1")
    request = urllib.request.Request(url, headers={"Authorization": f"Bearer {token}",
                                                  "Accept": "application/vnd.github+json"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            runs = json.load(response)["workflow_runs"]
    except urllib.error.HTTPError as error:
        if error.code == 404:  # New workflow: no runs yet.
            return None
        raise
    return runs[0]["head_sha"] if runs else None


def select(args):
    config = configuration()
    event_name = os.environ.get("GITHUB_EVENT_NAME", "")
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text()) if os.environ.get("GITHUB_EVENT_PATH") else {}
    choice = os.environ.get("SELECTED_SERVICE", "changed") or "changed"
    if event_name != "workflow_dispatch":
        choice = "changed"
    if choice not in ["changed", "all", *config["services"]]:
        raise ValueError("Unknown component")
    if args.base:
        base = args.base
    elif event_name == "pull_request":
        base = run(["git", "merge-base", event["pull_request"]["base"]["sha"], "HEAD"], capture=True).strip()
    else:
        base = github_baseline()
    if base and set(base) != {"0"}:
        files = run(["git", "diff", "--no-renames", "--name-only", "-z", base, "HEAD"], capture=True).split("\0")
        services = affected_services(files, config)
    else:
        # Manual first release explicitly picks a component or 'all'.
        # A newly created main branch must check and release its product code.
        files = run(["git", "ls-files", "-z"], capture=True).split("\0")
        services = affected_services(files, config)
    if choice == "all":
        services = sorted(config["services"])
    elif choice != "changed":
        services = [choice]
    checks = services
    if any(file.startswith(("scripts/ci/", ".github/")) for file in files):
        checks = sorted(config["services"])
    result = {"services": services, "checks": checks}
    print(json.dumps(result))
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a") as output:
            for name, values in result.items():
                output.write(f"{name}={json.dumps(values, separators=(',', ':'))}\n")


def check(name):
    service = configuration()["services"][name]
    if service.get("frontend"):
        folder = ROOT / service["frontend"]
        run(["npm", "ci"], cwd=folder)
        run(["npm", "run", "build"], cwd=folder)
    else:
        run(["dotnet", "build", service["project"], "-c", "Release", "-m:1"])
    for command in service.get("tests", []):
        run(command)


def latest_main():
    repository = os.environ["GITHUB_REPOSITORY"]
    request = urllib.request.Request(
        f"https://api.github.com/repos/{repository}/git/ref/heads/main",
        headers={"Authorization": f"Bearer {os.environ['GH_TOKEN']}",
                 "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)["object"]["sha"]


def deploy(services):
    if os.environ.get("GITHUB_REF") != "refs/heads/main":
        raise ValueError("Production deployments must use main")
    # A queued old run must not replace a newer main commit.
    latest = latest_main()
    sha = os.environ["GITHUB_SHA"]
    if latest != sha:
        raise RuntimeError("Main has advanced; this stale run must not count as a successful release.")
    config = configuration()
    tag = f"{sha[:12]}-{os.environ['GITHUB_RUN_ID']}-{os.environ['GITHUB_RUN_ATTEMPT']}"
    images = {}
    for name in services:
        service = config["services"][name]
        image = f"{config['region']}-docker.pkg.dev/{config['project']}/{config['registry']}/{name}:{tag}"
        command = ["docker", "build", "-f", service["dockerfile"], "-t", image]
        command += service.get("docker_args", [])
        for key, value in service.get("build_args", {}).items():
            if value.startswith("env:"):
                value = os.environ.get(value[4:], "")
                if not value:
                    raise ValueError(f"Missing browser build setting for {name}: {key}")
            command += ["--build-arg", f"{key}={value}"]
        run(command + ["."])
        run(["docker", "push", image])
        images[name] = image
    # Build all selected images before making runtime changes.
    for name, image in images.items():
        if config["platform"] == "gke":
            namespace = config["namespace"]
            previous = run(["kubectl", "get", "deployment", name, "-n", namespace,
                            "-o", f"jsonpath={{.spec.template.spec.containers[?(@.name=='{name}')].image}}"], capture=True).strip()
            previous_routes = None
            try:
                if name == "gateway":
                    previous_routes = json.loads(run(["kubectl", "get", "configmap", "gateway-routes", "-n", namespace, "-o", "json"], capture=True))["data"]
                    routes = (ROOT / "deploy/docker/ocelot.json").read_text()
                    run(["kubectl", "patch", "configmap", "gateway-routes", "-n", namespace, "--type=merge", "--patch", json.dumps({"data": {"ocelot.json": routes}})])
                run(["kubectl", "set", "image", f"deployment/{name}", f"{name}={image}", "-n", namespace])
                run(["kubectl", "rollout", "status", f"deployment/{name}", "-n", namespace, "--timeout=600s"])
                if config["services"][name].get("health_url"):
                    healthy(config["services"][name]["health_url"])
            except (subprocess.CalledProcessError, RuntimeError):
                if previous_routes is not None:
                    run(["kubectl", "patch", "configmap", "gateway-routes", "-n", namespace, "--type=merge", "--patch", json.dumps({"data": previous_routes})])
                if previous:
                    run(["kubectl", "set", "image", f"deployment/{name}", f"{name}={previous}", "-n", namespace])
                    run(["kubectl", "rollout", "status", f"deployment/{name}", "-n", namespace, "--timeout=600s"])
                raise
        else:
            base = ["gcloud", "run", "services"]
            flags = [f"--project={config['project']}", f"--region={config['region']}", "--quiet"]
            previous = json.loads(run(base + ["describe", name, "--format=json(status.traffic)"] + flags, capture=True))
            try:
                run(base + ["update", name, f"--image={image}"] + flags)
                url = run(base + ["describe", name, "--format=value(status.url)"] + flags, capture=True).strip()
                healthy(url + config["services"][name].get("health_path", "/health"))
            except (subprocess.CalledProcessError, RuntimeError):
                traffic = [f"{item['revisionName']}={item['percent']}" for item in previous["status"]["traffic"] if item.get("percent")]
                if traffic:
                    run(base + ["update-traffic", name, "--to-revisions=" + ",".join(traffic)] + flags)
                raise
        message = f"Deployed {name}: `{image}`"
        print(message)
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as summary:
                summary.write(f"- {message}\n")


def healthy(url):
    for attempt in range(6):
        try:
            with urllib.request.urlopen(url, timeout=20) as response:
                if response.status == 200:
                    return
        except (urllib.error.URLError, TimeoutError):
            pass
        if attempt < 5:
            time.sleep(10)
    raise RuntimeError(f"Deployment health check failed: {url}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["select", "check", "deploy"])
    parser.add_argument("service", nargs="?")
    parser.add_argument("--base")
    args = parser.parse_args()
    if args.command == "select":
        select(args)
    elif args.command == "check":
        check(args.service)
    else:
        services = json.loads(os.environ["DEPLOY_SERVICES"])
        if not isinstance(services, list) or any(name not in configuration()["services"] for name in services):
            raise ValueError("Invalid service selection")
        deploy(services)


if __name__ == "__main__":
    main()
