"""Create/reuse clearly labelled local QA agency and account; never sends email."""
from pathlib import Path
import runpy
import secrets
helpers = runpy.run_path(str(Path(__file__).with_name('verify-agency-access.py')))
request, login, check = (helpers[name] for name in ('request', 'login', 'check'))
token = login('agency.admin@agency.local')
status, agencies = request(5003, '/api/crm/agencies/roster', token)
check('Read agency roster', status == 200)
name = 'QA Local Subagency Verification'
agency = next((agency for agency in agencies if agency['name'] == name), None)
if agency is None:
    status, agency = request(5003, '/api/crm/agencies', token, {'name': name, 'contactEmail': 'qa-subagency@example.invalid', 'brokerageLicense': 'LOCAL-QA', 'parentAgencyId': 'AG-0002'})
    check('Create subagency without tenant ID', status == 200)
check('Parent forced to administrators own agency', agency['parentAgencyId'] == 'AG-0001')
status, scope = request(5001, '/api/auth/visible-tenants', token)
check('New tenant synchronized with Identity', status == 200 and agency['tenantId'] in scope)
status, users = request(5001, '/api/auth/users', token)
email = 'qa-subagency-user@example.invalid'
if not any(user['email'] == email for user in users):
    status, _ = request(5001, '/api/auth/register-user', token, {'fullName': 'QA Branch Broker', 'email': email, 'passwordHash': secrets.token_urlsafe(24), 'systemRole': 'Broker', 'tenantId': agency['tenantId']})
    check('Create subagency user', status == 200)
status, users = request(5001, '/api/auth/users', token)
check('Subagency user visible in managed roster', status == 200 and any(user['email'] == email and user['tenantId'] == agency['tenantId'] for user in users))
status, _ = request(5001, '/api/auth/register-user', token, {'fullName': 'Forbidden QA role', 'email': 'qa-forbidden-role@example.invalid', 'passwordHash': secrets.token_urlsafe(24), 'systemRole': 'PlatformAdmin', 'tenantId': agency['tenantId']})
check('Platform role creation denied', status == 403)
user = next(user for user in users if user['email'] == email)
status, brokers = request(5003, '/api/crm/brokers?page=1&tenantId=' + agency['tenantId'], token)
check('Read subagency brokers', status == 200)
if not any(broker['brokerId'] == user['userId'] for broker in brokers):
    status, broker = request(5003, '/api/crm/brokers', token, {'brokerId': user['userId'], 'tenantId': agency['tenantId'], 'agencyId': agency['agencyId'], 'fullName': 'QA Branch Broker', 'email': email})
    check('Create broker in selected subagency', status == 200 and broker['tenantId'] == agency['tenantId'])
from datetime import date
today = date.today()
status, _ = request(5003, '/api/crm/broker-targets', token, {'brokerId': user['userId'], 'year': today.year, 'month': today.month, 'targetCommission': 1000})
check('Save new brokers target', status == 200)
status, targets = request(5003, '/api/crm/broker-targets?brokerId=' + user['userId'] + '&tenantId=' + agency['tenantId'], token)
check('Target belongs to selected broker and subagency', status == 200 and any(target['brokerId'] == user['userId'] and target['targetCommission'] == 1000 for target in targets))
status, targets = request(5003, '/api/crm/broker-targets?brokerId=' + user['userId'] + '&tenantId=TEN-0002', token)
check('Unrelated tenant target filter excluded', status == 200 and targets == [])
