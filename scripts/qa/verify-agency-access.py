"""Local API smoke checks. Supply QA_PASSWORD locally; never prints tokens."""
import json
import os
import urllib.error
import urllib.request

password = os.environ.get('QA_PASSWORD', 'Password123!')

def request(port, path, token=None, data=None, method=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    call = urllib.request.Request('http://localhost:' + str(port) + path, headers=headers, data=json.dumps(data).encode() if data is not None else None, method=method)
    try:
        with urllib.request.urlopen(call, timeout=40) as response:
            body = response.read()
            return response.status, json.loads(body) if body else None
    except urllib.error.HTTPError as error:
        return error.code, None

def login(email):
    status, body = request(5001, '/api/auth/login', data={'email': email, 'password': password})
    assert status == 200, ('login', status)
    return body['token']

def check(label, condition):
    print(label + ': ' + ('PASS' if condition else 'FAIL'), flush=True)
    assert condition, label

def verify_access():
    admin = login('agency.admin@agency.local')
    status, scope = request(5001, '/api/auth/visible-tenants', admin)
    check('Own tenant retained', status == 200 and 'TEN-0001' in scope)
    status, board = request(5003, '/api/crm/boards?page=1', admin)
    check('Agency board accessible', status == 200)
    print('Visible leads/deals:', len(board['unverifiedLeads']), len(board['verifiedDeals']))
    status, board = request(5003, '/api/crm/boards?page=1&tenantId=TEN-0002', admin)
    check('Unrelated agency board excluded', status == 200 and not board['unverifiedLeads'] and not board['verifiedDeals'])
    status, contacts = request(5003, '/api/crm/contacts?page=1&tenantId=TEN-0002', admin)
    check('Unrelated contacts excluded', status == 200 and contacts == [])
    status, properties = request(5002, '/api/properties/1000?tenantId=TEN-0002', admin)
    check('Unrelated property filter denied', status == 404)
    status, property_record = request(5002, '/api/properties/1000', admin)
    check('Own property accessible', status == 200)
    status, users = request(5001, '/api/auth/users', admin)
    check('Own user in managed roster', status == 200 and any(user['email'] == 'agency.admin@agency.local' for user in users))
    broker = login('broker@agency.local')
    status, profile = request(5001, '/api/user/profile', broker)
    check('Broker profile available', status == 200 and profile['systemRole'] == 'Broker')
    status, _ = request(5003, '/api/crm/agency/glance/summary', broker)
    check('Agency-wide finances protected', status == 403)

if __name__ == '__main__':
    verify_access()
