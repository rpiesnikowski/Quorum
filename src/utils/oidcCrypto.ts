import { DecodedJwt } from '../types/oidc';

/**
 * Generuje kryptograficznie silny ciąg losowych znaków (dla state, nonce, code_verifier)
 */
export function generateRandomString(length: number = 48): string {
  const charset = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~';
  const randomValues = new Uint8Array(length);
  if (typeof window !== 'undefined' && window.crypto) {
    window.crypto.getRandomValues(randomValues);
  } else {
    for (let i = 0; i < length; i++) {
      randomValues[i] = Math.floor(Math.random() * 256);
    }
  }
  let result = '';
  for (let i = 0; i < length; i++) {
    result += charset[randomValues[i] % charset.length];
  }
  return result;
}

/**
 * Konwertuje bufor bajtów na ciąg Base64URL (RFC 7636) bez dopełnienia '='
 */
function bufferToBase64Url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

/**
 * Generuje challenge PKCE SHA-256 (S256) na podstawie verifiera
 */
export async function generateCodeChallengeS256(verifier: string): Promise<string> {
  if (typeof window === 'undefined' || !window.crypto || !window.crypto.subtle) {
    // Prosty fallback, jeśli Crypto Subtle niedostępne
    return 'fallback_challenge_' + btoa(verifier).replace(/[^a-zA-Z0-9]/g, '').substring(0, 32);
  }
  const encoder = new TextEncoder();
  const data = encoder.encode(verifier);
  const hash = await window.crypto.subtle.digest('SHA-256', data);
  return bufferToBase64Url(hash);
}

/**
 * Generuje parę PKCE: code_verifier i code_challenge (S256)
 */
export async function generatePkcePair(): Promise<{ verifier: string; challenge: string }> {
  const verifier = generateRandomString(64);
  const challenge = await generateCodeChallengeS256(verifier);
  return { verifier, challenge };
}

/**
 * Bezpieczne dekodowanie ciągu Base64 / Base64URL
 */
function base64UrlDecode(str: string): string {
  let base64 = str.replace(/-/g, '+').replace(/_/g, '/');
  while (base64.length % 4) {
    base64 += '=';
  }
  try {
    return decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
  } catch {
    return atob(base64);
  }
}

/**
 * Parsuje i dekoduje token JWT (JSON Web Token)
 */
export function parseAndDecodeJwt(token: string): DecodedJwt | null {
  if (!token || typeof token !== 'string') return null;
  const parts = token.trim().split('.');
  if (parts.length !== 3) {
    return {
      header: {},
      payload: { error: 'Nieprawidłowy format JWT (wymagane 3 segmenty oddzielone kropką)' },
      signature: '',
      raw: token,
      isValidStructure: false
    };
  }

  try {
    const headerJson = base64UrlDecode(parts[0]);
    const payloadJson = base64UrlDecode(parts[1]);

    const header = JSON.parse(headerJson);
    const payload = JSON.parse(payloadJson);

    let expiresAt: Date | undefined;
    let isExpired: boolean | undefined;

    if (payload.exp && typeof payload.exp === 'number') {
      expiresAt = new Date(payload.exp * 1000);
      isExpired = expiresAt.getTime() < Date.now();
    }

    return {
      header,
      payload,
      signature: parts[2],
      raw: token,
      isValidStructure: true,
      expiresAt,
      isExpired
    };
  } catch (err) {
    return {
      header: {},
      payload: { error: 'Błąd dekodowania zawartości JWT', details: String(err) },
      signature: parts[2] || '',
      raw: token,
      isValidStructure: false
    };
  }
}

/**
 * Formatuje timestamp wygaśnięcia JWT na czytelny tekst
 */
export function formatJwtExpiry(exp?: number): string {
  if (!exp) return 'Brak informacji o wygaśnięciu (brak pola exp)';
  const date = new Date(exp * 1000);
  const now = Date.now();
  const diffMs = date.getTime() - now;

  const formattedDate = date.toLocaleString('pl-PL', {
    dateStyle: 'medium',
    timeStyle: 'medium'
  });

  if (diffMs <= 0) {
    return `Wygasł (${formattedDate})`;
  }

  const minutes = Math.floor(diffMs / 60000);
  const seconds = Math.floor((diffMs % 60000) / 1000);

  if (minutes > 60) {
    const hours = Math.floor(minutes / 60);
    return `Wygasa za ~${hours}h ${minutes % 60}m (${formattedDate})`;
  }

  return `Wygasa za ${minutes}m ${seconds}s (${formattedDate})`;
}

/**
 * Generuje realistyczny mock token JWT dla celów testowania symulacyjnego
 */
export function createMockJwt(headerObj: Record<string, unknown>, payloadObj: Record<string, unknown>): string {
  const b64Header = btoa(JSON.stringify(headerObj))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');

  const b64Payload = btoa(JSON.stringify(payloadObj))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');

  const mockSignature = 'kX7z8Q_mockSignatureQuorum' + Math.random().toString(36).substring(2, 15);
  return `${b64Header}.${b64Payload}.${mockSignature}`;
}
