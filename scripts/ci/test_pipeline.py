#!/usr/bin/env python3
"""Regression checks for deployment scope, including shared dependencies."""
import unittest
import os
import subprocess
from unittest.mock import patch
from pipeline import affected_services, configuration, project_inputs, deploy


class ChangeDetectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = configuration()
        cls.land = cls.config["platform"] == "gke"

    def select(self, *files):
        return affected_services(files, self.config)

    def test_documentation_does_not_release_apps(self):
        self.assertEqual(self.select("README.md", "docs/google-cloud.md", "AGENTS.md"), [])

    def test_frontend_changes_are_isolated(self):
        folder = "frontend" if self.land else "frontend-client"
        name = "frontend" if self.land else "frontend-client"
        self.assertEqual(self.select(f"{folder}/src/App.tsx"), [name])

    def test_shared_libraries_include_dependents(self):
        if self.land:
            self.assertEqual(self.select("backend/shared/Cadastre.Tenancy.Core/Tenant.cs"), ["identity", "land"])
            self.assertEqual(self.select("backend/shared/Cadastre.Reporting.Core/Report.cs"), ["land", "reporting"])
            self.assertEqual(self.select("backend/shared/Cadastre.Land.Domain/ParcelTopology.cs"), ["land"])
        else:
            self.assertEqual(self.select("backend-services/Shared.Access/Permissions.cs"), ["crm-service", "developer-api", "identity-service", "property-service"])
            self.assertEqual(self.select("backend-services/Shared.Presentation/Pdf.cs"), ["crm-service", "developer-api", "identity-service"])

    def test_backend_dockerfile_selects_its_consumers(self):
        if self.land:
            self.assertEqual(self.select("deploy/docker/backend.Dockerfile"), ["gateway", "identity", "land", "payments", "reporting"])
        else:
            self.assertEqual(self.select("backend-services/CRM.Microservice/Dockerfile"), ["crm-service"])

    def test_build_context_changes_include_all_images(self):
        self.assertEqual(self.select(".dockerignore"), sorted(self.config["services"]))

    def test_local_configurations_are_not_deployed(self):
        path = "backend/services/Cadastre.Land.Api/appsettings.Development.json" if self.land else "backend-services/Identity.Microservice/appsettings.Development.json"
        self.assertEqual(self.select(path), [])

    def test_special_runtime_assets_are_included(self):
        if self.land:
            self.assertEqual(self.select("scripts/gis/countries.json"), ["land"])
            self.assertEqual(self.select("deploy/docker/ocelot.json"), ["gateway"])
            self.assertEqual(self.select("deploy/docker/frontend.conf.template"), ["frontend"])
        else:
            self.assertEqual(self.select("client-facing-app/app/solutions/page.tsx"), ["client-facing"])
            self.assertEqual(self.select("frontend-client/public/logo.jpg"), ["developer-api", "frontend-client", "identity-service"])

    def test_public_website_and_developer_api_are_release_components(self):
        self.assertEqual(self.select("Developer.API/Program.cs"), ["developer-api"])
        self.assertEqual(self.select("property-public-site/server.mjs"), ["property-public-site"])

    def test_project_references_are_resolved(self):
        for service in self.config["services"].values():
            if service.get("project"):
                self.assertTrue(project_inputs(service["project"]))


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.sha = "a" * 40
        self.config = {"project": "example", "registry": "images", "region": "africa-south1",
                       "platform": "gke", "namespace": "test", "services": {"frontend": {"dockerfile": "Dockerfile"}}}
        self.environment = {"GITHUB_REF": "refs/heads/main", "GITHUB_SHA": self.sha,
                            "GITHUB_RUN_ID": "12", "GITHUB_RUN_ATTEMPT": "1"}

    def test_non_main_deployment_is_rejected(self):
        with patch.dict(os.environ, {"GITHUB_REF": "refs/heads/feature"}):
            with self.assertRaises(ValueError):
                deploy(["frontend"])

    def test_stale_run_does_not_overwrite_newer_main(self):
        with patch.dict(os.environ, self.environment), patch("pipeline.latest_main", return_value="b" * 40), patch("pipeline.run") as execute:
            with self.assertRaises(RuntimeError):
                deploy(["frontend"])
            execute.assert_not_called()

    def test_build_failure_never_mutates_runtime(self):
        def execute(args, **kwargs):
            if args[0] == "git":
                return self.sha
            raise subprocess.CalledProcessError(1, args)
        with patch.dict(os.environ, self.environment), patch("pipeline.latest_main", return_value=self.sha), patch("pipeline.configuration", return_value=self.config), patch("pipeline.run", side_effect=execute) as commands:
            with self.assertRaises(subprocess.CalledProcessError):
                deploy(["frontend"])
            self.assertFalse(any(call.args[0][0] == "kubectl" for call in commands.call_args_list))

    def test_failed_rollout_restores_previous_image(self):
        rollouts = 0
        def execute(args, **kwargs):
            nonlocal rollouts
            if args[0] == "git":
                return self.sha
            if args[:3] == ["kubectl", "get", "deployment"]:
                return "previous-image"
            if args[:3] == ["kubectl", "rollout", "status"]:
                rollouts += 1
                if rollouts == 1:
                    raise subprocess.CalledProcessError(1, args)
            return ""
        with patch.dict(os.environ, self.environment), patch("pipeline.latest_main", return_value=self.sha), patch("pipeline.configuration", return_value=self.config), patch("pipeline.run", side_effect=execute) as commands:
            with self.assertRaises(subprocess.CalledProcessError):
                deploy(["frontend"])
            changes = [call.args[0] for call in commands.call_args_list if call.args[0][:3] == ["kubectl", "set", "image"]]
            self.assertEqual(len(changes), 2)
            self.assertEqual(changes[1][4], "frontend=previous-image")


if __name__ == "__main__":
    unittest.main()
