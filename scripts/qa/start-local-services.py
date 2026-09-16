"""Start verified local builds on their usual ports; keep secrets out of output."""
import json
import os
from pathlib import Path
import socket
import subprocess

root = Path(__file__).resolve().parents[2]
started = {}
for service, port in [('Identity', 5001), ('Property', 5002), ('CRM', 5003), ('Gateway', 5000)]:
    with socket.socket() as probe:
        if probe.connect_ex(('127.0.0.1', port)) == 0:
            print(f'{service}: port {port} already in use; left unchanged')
            continue
    folder = root / 'backend-services' / f'{service}.Microservice'
    assembly = f'{service}.Microservice.dll'
    if service == 'Gateway':
        folder = root / 'api-gateway'
        assembly = 'api-gateway.dll'
    binary = folder / 'bin/Debug/net10.0' / assembly
    if not binary.exists():
        raise SystemExit(f'Build {service} before starting it.')
    environment = os.environ.copy()
    dotenv = folder / '.env'
    if dotenv.exists():
        for raw_line in dotenv.read_text().splitlines():
            line = raw_line.strip()
            if not line or line.startswith('#') or '=' not in line:
                continue
            key, value = line.split('=', 1)
            environment[key.strip()] = value.strip().strip('\"\'')
    environment.update(IDENTITY_HOST='localhost', PROPERTY_HOST='localhost', CRM_HOST='localhost', DEVELOPER_HOST='localhost')
    environment.update(ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=f'http://localhost:{port}', Identity__BaseUrl='http://localhost:5001', Gateway__BaseUrl='http://localhost:5000/gateway', Email__Host='')
    with open(f'/tmp/resume-{service.lower()}.log', 'w') as log:
        process = subprocess.Popen(['dotnet', str(binary)], cwd=folder, env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    started[service] = {'pid': process.pid, 'port': port}
print(json.dumps(started))
print('Local QA email delivery is disabled. Logs: /tmp/resume-<service>.log')
