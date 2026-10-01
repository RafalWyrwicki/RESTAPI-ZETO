import http from "k6/http";
import { check } from "k6";

const baseUrl = (__ENV.BASE_URL || "https://localhost:44379").replace(/\/$/, "");
const apiToken = __ENV.API_TOKEN;
const printerIp = __ENV.PRINTER_IP;

if (!apiToken || !printerIp) {
  throw new Error("Ustaw zmienne API_TOKEN i PRINTER_IP przed uruchomieniem testu.");
}

export const options = {
  scenarios: {
    api_load: {
      executor: "constant-vus",
      vus: Number(__ENV.VUS || 60),
      duration: __ENV.DURATION || "30s",
      gracefulStop: "10s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<5000"],
  },
};

export default function () {
  const identifier = ((__VU * 1000 + __ITER) % 9999) + 1;
  const payload = JSON.stringify({
    Identyfikator: identifier,
    Wydzial: "LOAD",
    Repertorium: "TEST",
    Nr_Sprawy: identifier,
    Rok: 26,
    Tom: "1",
    IP_Drukarka: printerIp,
    Sygnatura_Text: `LOAD TEST ${identifier}/26`,
    Sygnatura_Barcode: `LOAD${identifier}26`,
  });

  const response = http.post(`${baseUrl}/api/dane`, payload, {
    headers: {
      Authorization: `Bearer ${apiToken}`,
      "Content-Type": "application/json",
      Accept: "application/json",
      "X-Correlation-ID": `k6-${__VU}-${__ITER}`,
    },
    timeout: "20s",
    tags: { endpoint: "POST /api/dane" },
  });

  check(response, {
    "status 200": (result) => result.status === 200,
    "brak 429": (result) => result.status !== 429,
    "brak 5xx": (result) => result.status < 500,
  });
}
