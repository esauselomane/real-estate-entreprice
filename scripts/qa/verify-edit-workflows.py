"""Check edit contracts against local demo records without changing their values."""
from pathlib import Path
import runpy

helpers = runpy.run_path(str(Path(__file__).with_name('verify-agency-access.py')))
request, login, check = (helpers[name] for name in ('request', 'login', 'check'))
admin = login('agency.admin@agency.local')
status, board = request(5003, '/api/crm/boards?page=1', admin)
check('Load agency board', status == 200)
stages = ['DiscoveryNeedsAnalysis', 'Presentation', 'ProposalQuoteSent', 'Negotiation', 'ClosedWon', 'ClosedLost']
open_deals = [deal for deal in board['verifiedDeals'] if deal['dealStage'] not in [4, 5, 'ClosedWon', 'ClosedLost']]
check('Open demo deal available', bool(open_deals))
deal = open_deals[0]
stage = stages[deal['dealStage']] if isinstance(deal['dealStage'], int) else deal['dealStage']
status, updated = request(5003, '/api/crm/deal/' + str(deal['dealId']), admin, {'dealStage': stage, 'transactionValue': deal['transactionValue']}, 'PUT')
check('Save unchanged deal using named stage', status == 200)
status, contacts = request(5003, '/api/crm/contacts?page=1', admin)
check('Load agency contacts', status == 200 and bool(contacts))
contact = next((contact for contact in contacts if not contact.get('associatedCompanyId') and not contact.get('associatedLegalEntityId')), None)
check('Contact without company relationship available', contact is not None)
payload = {key: value for key, value in contact.items() if key not in ['tenantId', 'companyDetails', 'legalEntityDetails', 'agency']}
flags = contact['contactType']
if isinstance(flags, int):
    payload['contactType'] = ','.join(name for number, name in [(1, 'Buyer'), (2, 'Seller'), (4, 'Broker'), (8, 'Owner'), (16, 'Tenant'), (32, 'Staff'), (64, 'Manager')] if flags & number) or 'None'
payload['associatedLegalEntityId'] = ''
status, updated = request(5003, '/api/crm/contacts/' + contact['contactId'], admin, payload, 'PUT')
check('Save contact with named types and empty optional relation', status == 200)
check('Contact tenant unchanged', updated['tenantId'] == contact['tenantId'])
broker = login('broker@agency.local')
status, profile = request(5001, '/api/user/profile', broker)
check('Read broker profile', status == 200)
status, _ = request(5001, '/api/user/profile', broker, {'fullName': profile['fullName'], 'licenseNumber': profile['licenseNumber'], 'email': 'forbidden@example.invalid', 'systemRole': 'PlatformAdmin', 'tenantId': 'forbidden'}, 'PUT')
check('Broker can save own profile', status == 200)
_, updated = request(5001, '/api/user/profile', broker)
check('Email, role and tenant immutable', all(profile[key] == updated[key] for key in ('email', 'systemRole', 'tenantId')))
