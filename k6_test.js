import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    idempotency_race_condition: {
      executor: 'shared-iterations',
      vus: 50,
      iterations: 200,
      maxDuration: '10s',
      exec: 'testIdempotency',
    },
    concurrent_transfers_deadlock: {
      executor: 'shared-iterations',
      vus: 30,
      iterations: 300,
      maxDuration: '10s',
      exec: 'testTransfers',
      startTime: '2s',
    },
  },
};

const BASE_URL = 'http://localhost:5270';

export function setup() {
  http.post(`${BASE_URL}/reset`);
  
  const headers = { 'Content-Type': 'application/json' };
  http.post(`${BASE_URL}/event`, JSON.stringify({ type: 'deposit', destination: 'K6_ACC_A', amount: 1000 }), { headers });
  http.post(`${BASE_URL}/event`, JSON.stringify({ type: 'deposit', destination: 'K6_ACC_B', amount: 1000 }), { headers });
}

export function testIdempotency() {
  const payload = JSON.stringify({
    type: 'deposit',
    destination: 'K6_IDM_ACCOUNT',
    amount: 100,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': 'k6-idempotency-key-2026',
    },
  };

  const res = http.post(`${BASE_URL}/event`, payload, params);
  
  check(res, {
    'idempotency: status is 201': (r) => r.status === 201,
    'idempotency: response has K6_IDM_ACCOUNT': (r) => r.body.includes('K6_IDM_ACCOUNT'),
  });
}

export function testTransfers() {
  const isReverse = __VU % 2 === 0;
  const origin = isReverse ? 'K6_ACC_B' : 'K6_ACC_A';
  const destination = isReverse ? 'K6_ACC_A' : 'K6_ACC_B';

  const payload = JSON.stringify({
    type: 'transfer',
    origin: origin,
    destination: destination,
    amount: 1,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  const res = http.post(`${BASE_URL}/event`, payload, params);
  
  check(res, {
    'transfer: status is 201 or 404 (if insufficient funds)': (r) => r.status === 201 || r.status === 404,
  });
}
