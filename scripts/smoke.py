"""End-to-end checks against a running app and a disposable SQL database.
Usage: MYAIMACHINE_ADMIN_KEY=... python3 scripts/smoke.py
Creates training data, retrains the shared model, and creates/deletes a conversation.
"""
import http.cookiejar
import json
import os
import re
import urllib.error
import urllib.request
import uuid

base = os.environ.get('MYAIMACHINE_URL', 'http://localhost:5080')
key = os.environ['MYAIMACHINE_ADMIN_KEY']

def client():
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    html = opener.open(base).read().decode()
    token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html).group(1)
    def call(path, method='GET', data=None, admin=False, csrf=True):
        headers = {'Content-Type': 'application/json'}
        if csrf:
            headers['X-CSRF-TOKEN'] = token
        if admin:
            headers['X-Admin-Key'] = key
        request = urllib.request.Request(base + path, data=None if data is None else json.dumps(data).encode(), headers=headers, method=method)
        try:
            with opener.open(request) as response:
                body = response.read()
                return response.status, json.loads(body) if body else None
        except urllib.error.HTTPError as error:
            return error.code, error.read().decode()
    call('/api/chat')  # Establish independent owner session.
    return call

first, second = client(), client()
assert first('/api/training')[0] == 401
assert first('/api/chat', 'POST', {'message': 'سلام'}, csrf=False)[0] == 400
assert first('/api/chat', 'POST', {'message': ' '})[0] == 400
sample = 'آزمون ' + uuid.uuid4().hex
assert first('/api/training/examples', 'POST', {'input': sample, 'response': 'پاسخ آزمایشی'}, True)[0] == 200
assert first('/api/training/train', 'POST', admin=True)[0] == 200
status, reply = first('/api/chat', 'POST', {'message': sample})
assert status == 200 and reply['text'] == 'پاسخ آزمایشی' and reply['matched']
id = reply['conversationId']
assert len(first('/api/chat/' + id)[1]) == 2
assert second('/api/chat/' + id)[0] == 404
assert second('/api/chat/' + id, 'DELETE')[0] == 404
assert first('/api/chat/' + id, 'DELETE')[0] == 204
assert first('/api/chat/' + id)[0] == 404
print('PASS: training, prediction, persistence, session isolation, CSRF, validation, deletion')
