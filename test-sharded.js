// Fil: search-test.js
import http from 'k6/http';
import { sleep, check } from 'k6';

// En liste af test-søgeord for at undgå caching
const searchTerms = [
  'coke', 
  'weed', 
  'mercedes', 
  'computer', 
  'hest'
];

// 'options' definerer din test-profil
export const options = {
  // High load test  
  vus: 25,           // 25000 virtuelle brugere (samtidige forbindelser)
  duration: '2m',    // Kør testen i 20 minutter
};

// Dette er den funktion, hver virtuel bruger kører igen og igen
export default function () {
  
  // 1. Vælg et tilfældigt søgeord
  const term = searchTerms[Math.floor(Math.random() * searchTerms.length)];
  
  // 2. Use port-forwarded SearchWeb for sharded namespace
  const url = `http://127.0.0.1:8080/?SearchQuery=${encodeURIComponent(term)}&MaxResults=10`;

  // 3. Send HTTP GET request
  const res = http.get(url);

  // 4. Tjek om forespørgslen var succesfuld (HTTP 200 OK)
  check(res, {
    'status er 200': (r) => r.status === 200,
  });

  // 5. Vent 1 sekund før denne bruger kører igen
  sleep(1);
}