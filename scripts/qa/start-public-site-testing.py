"""Start the local public-site E2E stack using loopback SQL connections; never print secrets."""
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import time
import urllib.request

root = Path(__file__).resolve().parents[2]
settings = json.loads((root / 'Developer.API/appsettings.local.json').read_text())
connection = settings['ConnectionStrings']['DeveloperConnection']
server = re.search(r'(?:^|;)(?:Server|Data Source)=([^;]+)', connection, re.I)
if not server or server.group(1).split(',')[0].lower() not in ('localhost', '127.0.0.1', '.'):
    raise SystemExit('Only loopback SQL Server connections are permitted.')

def database_connection(database):
    return re.sub(r'(Database|Initial Catalog)=[^;]+', lambda match: match.group(1) + '=' + database, connection, flags=re.I)

def listening(port):
    with socket.socket() as probe:
        return probe.connect_ex(('127.0.0.1', port)) == 0

services = [
    ('identity', 'backend-services/Identity.Microservice', 'Identity.Microservice.dll', 5001, 'RealEstateIdentityDB'),
    ('property', 'backend-services/Property.Microservice', 'Property.Microservice.dll', 5002, 'RealEstatePropertyDB'),
    ('crm', 'backend-services/CRM.Microservice', 'CRM.Microservice.dll', 5003, 'RealEstateCRMDB'),
    ('gateway', 'api-gateway', 'api-gateway.dll', 5000, None),
    ('developer', 'Developer.API', 'Developer.API.dll', 5006, None),
]
for name, directory, assembly, port, database in services:
    if listening(port):
        print(f'{name}: using existing listener on {port}', flush=True)
        continue
    folder = root / directory
    binary = folder / 'bin/Debug/net10.0' / assembly
    if not binary.exists():
        raise SystemExit(f'Build {directory} first.')
    environment = os.environ.copy()
    environment.update(ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS=f'http://localhost:{port}',
        IDENTITY_HOST='localhost', PROPERTY_HOST='localhost', CRM_HOST='localhost', DEVELOPER_HOST='localhost',
        Identity__BaseUrl='http://localhost:5001', Gateway__BaseUrl='http://localhost:5000/gateway',
        Email__Host='', Email__SmtpHost='', SMTP_HOST='')
    for key, value in settings.get('Jwt', {}).items():
        environment['Jwt__' + key] = str(value)
    if database:
        environment['ConnectionStrings__DefaultConnection'] = database_connection(database)
    with open(f'/tmp/public-e2e-{name}.log', 'w') as log:
        process = subprocess.Popen(['dotnet', str(binary)], cwd=folder, env=environment,
            stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    print(f'{name}: started PID {process.pid} on {port}', flush=True)

for name, directory, command, port in [
    ('public-site', 'property-public-site', ['npm', 'run', 'start'], 3010),
    ('portal', 'frontend-client', ['npm', 'run', 'dev', '--', '--host', 'localhost', '--port', '5173', '--strictPort'], 5173),
]:
    if not listening(port):
        with open(f'/tmp/public-e2e-{name}.log', 'w') as log:
            process = subprocess.Popen(command, cwd=root / directory, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        print(f'{name}: started PID {process.pid} on {port}', flush=True)

for port in [5001, 5002, 5003, 5000, 5006, 3010, 5173]:
    url = f'http://localhost:{port}' + ('' if port == 5173 else '/health')
    for attempt in range(45):
        try:
            with urllib.request.urlopen(url, timeout=2) as response:
                if response.status == 200:
                    print(f'Ready: {port}', flush=True)
                    break
        except Exception:
            time.sleep(1)
    else:
        raise SystemExit(f'Port {port} did not become healthy. See /tmp/public-e2e-<service>.log.')
