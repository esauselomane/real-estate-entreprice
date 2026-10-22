"""Exercise the labelled local public-site fixtures. Tokens and API keys are never printed."""
import json
import os
from pathlib import Path
import urllib.request
import urllib.error

root = Path(__file__).resolve().parents[2]
configuration = {}
for line in (root / 'property-public-site/.env').read_text().splitlines():
    if '=' in line and not line.lstrip().startswith('#'):
        key, value = line.split('=', 1)
        configuration[key] = value


def request(port, route, payload=None, method=None, headers=None):
    outgoing = {'Content-Type': 'application/json'}
    outgoing.update(headers or {})
    call = urllib.request.Request(f'http://localhost:{port}{route}',
        data=json.dumps(payload).encode() if payload is not None else None, headers=outgoing, method=method)
    try:
        with urllib.request.urlopen(call, timeout=30) as response:
            body = response.read()
            return response.status, json.loads(body) if body else None
    except urllib.error.HTTPError as error:
        return error.code, None


def check(label, condition):
    assert condition, label
    print('PASS: ' + label, flush=True)


public_headers = {'X-Api-Key': configuration['AGENCY_API_KEY'], 'X-Expected-Agency-Id': configuration['AGENCY_ID']}
status, result = request(5006, '/api/developer/properties?pageSize=48', headers=public_headers)
check('Developer API accepts the TEN-001 key', status == 200)
all_items = list(result['items'])
for page in range(2, (result['total'] + 47) // 48 + 1):
    status, extra = request(5006, '/api/developer/properties?pageSize=48&page=' + str(page), headers=public_headers)
    check('Additional public result page loads', status == 200)
    all_items.extend(extra['items'])
samples = [item for item in all_items if '[PUBLIC-SITE-E2E:' in item['description']]
check('Forty-eight approved sample listings are public', len(samples) == 48)
check('All six property types are available', {item['propertyType'] for item in samples} == {'Residential', 'Commercial', 'Retail', 'Industrial', 'Land', 'Room share/let'})
check('Images, units and owner contacts are populated', all(len(item['images']) == 3 and len(item['units']) == 2 and item['owner']['email'] == 'OWNER.PUBLIC-E2E@EXAMPLE.TEST' for item in samples))
status, result = request(5006, '/api/developer/properties/hot', headers=public_headers)
check('A pool of hot sample properties is available', status == 200 and result['total'] >= 5)
status, random_hot = request(5006, '/api/developer/properties/hot?pageSize=5&randomize=true', headers=public_headers)
check('Unfiltered hot endpoint supports five distinct property thumbnails', status == 200 and len(random_hot['items']) == 5 and len({item['id'] for item in random_hot['items']}) == 5 and all(item['isHot'] and item['thumb'] for item in random_hot['items']))
status, anonymous = request(3010, '/api/properties?pageSize=5')
check('Public website browsing works without authentication', status == 200 and len(anonymous['items']) == 5)
status, detail = request(3010, '/api/properties/' + str(anonymous['items'][0]['id']))
check('Property detail and owner contacts require no visitor login', status == 200 and bool(detail['owner']['email']))
status, result = request(5006, '/api/developer/properties?location=Sea%20Point&radiusKm=15&includeNearby=true&pageSize=48', headers=public_headers)
nearby_all_items = list(result['items'])
for page in range(2, (result['total'] + 47) // 48 + 1):
    status, extra = request(5006, '/api/developer/properties?pageSize=48&page=' + str(page), headers=public_headers)
    check('Additional public result page loads', status == 200)
    all_items.extend(extra['items'])
samples = [item for item in all_items if '[PUBLIC-SITE-E2E:' in item['description']]
check('Sea Point includes nearby Cape Town samples', status == 200 and len(nearby_samples) >= 8 and any(item['isNearby'] for item in nearby_samples))
check('Distant Pretoria sample is excluded from nearby search', not any('[PUBLIC-SITE-E2E:DISTANT]' in item['description'] for item in nearby_samples))
status, _ = request(5006, '/api/developer/properties')
check('Missing API key is rejected', status == 401)
status, _ = request(5006, '/api/developer/properties', headers={**public_headers, 'X-Expected-Agency-Id': 'TEN-0002'})
check('Wrong configured agency is rejected', status == 403)
for purpose, expected in [('Both', 2), ('Room', 2)]:
    status, filtered = request(5006, '/api/developer/properties?listingType=' + purpose, headers=public_headers)
    check(purpose + ' search returns the expected sample offerings', status == 200 and filtered['total'] >= expected)
for room_type in ['PrivateRoom', 'SharedRoom']:
    status, filtered = request(5006, '/api/developer/properties?listingType=Rent&propertyType=Room%20share%2Flet&roomLettingType=' + room_type, headers=public_headers)
    check(room_type + ' can be searched independently', status == 200 and filtered['total'] >= 1)
status, account = request(5006, '/api/developer/owners/login', {'email': 'owner.public-e2e@example.test', 'password': 'BiscoveSampleOwner2026!'}, headers=public_headers)
check('Sample owner can sign in', status == 200)
owner_headers = {**public_headers, 'X-Owner-Token': account['token']}
status, submissions = request(5006, '/api/developer/submissions', headers=owner_headers)
check('Owner workspace includes all fifty samples', status == 200 and len(submissions) == 50)
pending = next(item for item in submissions if '[PUBLIC-SITE-E2E:REVIEW]' in item['description'])
hidden = next(item for item in submissions if '[PUBLIC-SITE-E2E:HIDDEN]' in item['description'])
status, _ = request(5006, '/api/developer/properties/' + str(hidden['id']), headers=public_headers)
check('Unpublished sample detail returns 404', status == 404)
status, admin = request(5001, '/api/auth/login', {'email': os.environ.get('QA_ADMIN_EMAIL', 'admin@agency.local'), 'password': os.environ.get('QA_PASSWORD', 'Password123!')})
check('Existing SuperAdmin can sign in', status == 200)
admin_headers = {'Authorization': 'Bearer ' + admin['token']}
settings = {'isPublicListing': True, 'isHotProperty': True, 'ownerName': pending['owner']['name'], 'ownerEmail': pending['owner']['email'], 'ownerPhone': pending['owner']['phone']}
route = f"/api/properties/{pending['id']}/public-listing"
status, _ = request(5002, route, settings, method='PUT', headers=admin_headers)
check('Agency can approve and feature an owner submission', status == 200)
status, _ = request(5006, f"/api/developer/properties/{pending['id']}", headers=public_headers)
check('Approved submission becomes publicly available', status == 200)
settings['isPublicListing'] = False
status, _ = request(5002, route, settings, method='PUT', headers=admin_headers)
check('Agency can unpublish the sample', status == 200)
status, _ = request(5006, f"/api/developer/properties/{pending['id']}", headers=public_headers)
check('Unpublishing removes public detail immediately', status == 404)
payload = {key: pending[key] for key in ['address', 'description', 'propertyType', 'erfSize', 'isForSale', 'isForRent', 'salePrice', 'latitude', 'longitude', 'images']}
payload['units'] = [{key: unit[key] for key in ['number', 'description', 'areaSqM', 'rentalPrice', 'salePrice', 'advertisedDepositAmount', 'isVacant', 'images']} for unit in pending['units']]
payload['roomLettingType'] = pending['roomLettingType']
payload['publishContactConsent'] = True
status, _ = request(5006, f"/api/developer/submissions/{pending['id']}", payload, method='PUT', headers=owner_headers)
check('Owner can resubmit and restore the sample to Pending review', status == 200)
status, updated = request(5006, '/api/developer/submissions', headers=owner_headers)
reviewed = next(item for item in updated if item['id'] == pending['id'])
check('Review fixture remains pending for manual testing', reviewed['reviewStatus'] == 'Pending review')
status, _ = request(5006, '/api/developer/owners/logout', {}, headers=owner_headers)
check('Owner logout revokes the session', status == 204)
print('Local sample scenario is ready for manual end-to-end testing.')
