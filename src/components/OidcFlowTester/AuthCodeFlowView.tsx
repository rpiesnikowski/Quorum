import React, { useState } from 'react';
import { 
  Send, 
  ExternalLink, 
  Sparkles, 
  RefreshCw, 
  Key, 
  ShieldCheck, 
  Layers, 
  HelpCircle,
  Copy,
  Check,
  CheckSquare,
  Square
} from 'lucide-react';
import { AuthCodeConfig, OidcResponse, AVAILABLE_SCOPES } from '../../types/oidc';
import { RequestCodeViewer } from './RequestCodeViewer';
import { ResponseInspector } from './ResponseInspector';
import { 
  generatePkcePair, 
  generateRandomString, 
  parseAndDecodeJwt, 
  createMockJwt 
} from '../../utils/oidcCrypto';

interface AuthCodeFlowViewProps {
  config: AuthCodeConfig;
  onChangeConfig: (updated: Partial<AuthCodeConfig>) => void;
}

export const AuthCodeFlowView: React.FC<AuthCodeFlowViewProps> = ({
  config,
  onChangeConfig
}) => {
  const [copiedUrl, setCopiedUrl] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [response, setResponse] = useState<OidcResponse | null>(null);
  const [customScope, setCustomScope] = useState('');

  // Generowanie nowej pary PKCE, State i Nonce
  const handleRegenerateCrypto = async () => {
    const pkce = await generatePkcePair();
    const state = generateRandomString(24);
    const nonce = generateRandomString(24);
    const mockAuthCode = 'mock_code_' + generateRandomString(32);

    onChangeConfig({
      codeVerifier: pkce.verifier,
      codeChallenge: pkce.challenge,
      state,
      nonce,
      authCode: mockAuthCode
    });
  };

  // Budowa pełnego Authorization URL
  const buildAuthorizeUrl = (): string => {
    const baseUrl = config.issuerUrl.replace(/\/+$/, '') + (config.authorizeEndpoint.startsWith('/') ? config.authorizeEndpoint : '/' + config.authorizeEndpoint);
    const params = new URLSearchParams();
    params.set('client_id', config.clientId);
    params.set('redirect_uri', config.redirectUri);
    params.set('response_type', config.responseType);
    params.set('scope', config.scopes.join(' '));
    params.set('state', config.state);
    params.set('nonce', config.nonce);

    if (config.usePkce && config.codeChallenge) {
      params.set('code_challenge', config.codeChallenge);
      params.set('code_challenge_method', config.codeChallengeMethod);
    }

    if (config.responseMode && config.responseMode !== 'query') {
      params.set('response_mode', config.responseMode);
    }

    return `${baseUrl}?${params.toString()}`;
  };

  const authorizeUrl = buildAuthorizeUrl();

  // Budowa żądania Token Exchange (POST /connect/token)
  const tokenUrl = config.issuerUrl.replace(/\/+$/, '') + (config.tokenEndpoint.startsWith('/') ? config.tokenEndpoint : '/' + config.tokenEndpoint);

  const getFormData = (): Record<string, string> => {
    const data: Record<string, string> = {
      grant_type: 'authorization_code',
      client_id: config.clientId,
      code: config.authCode || 'MOCK_AUTH_CODE_XYZ',
      redirect_uri: config.redirectUri
    };

    if (config.usePkce && config.codeVerifier) {
      data.code_verifier = config.codeVerifier;
    }

    if (config.authMethod === 'client_secret_post' && config.clientSecret) {
      data.client_secret = config.clientSecret;
    }

    return data;
  };

  const formData = getFormData();
  const urlEncodedBody = new URLSearchParams(formData).toString();

  // Generowanie formatów kodu
  const generateRawHttp = (): string => {
    const parsedUrl = new URL(tokenUrl);
    let headers = `POST ${parsedUrl.pathname} HTTP/1.1\nHost: ${parsedUrl.host}\nContent-Type: application/x-www-form-urlencoded`;

    if (config.authMethod === 'client_secret_basic' && config.clientSecret) {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      headers += `\nAuthorization: Basic ${basic}`;
    }

    return `${headers}\n\n${urlEncodedBody}`;
  };

  const generateCurl = (): string => {
    let cmd = `curl -X POST "${tokenUrl}" \\\n  -H "Content-Type: application/x-www-form-urlencoded"`;
    if (config.authMethod === 'client_secret_basic' && config.clientSecret) {
      cmd += ` \\\n  -u "${config.clientId}:${config.clientSecret}"`;
    }
    cmd += ` \\\n  -d "${urlEncodedBody}"`;
    return cmd;
  };

  const generatePowerShell = (): string => {
    let script = `$body = @{\n`;
    for (const [k, v] of Object.entries(formData)) {
      script += `    "${k}" = "${v}"\n`;
    }
    script += `}\n\n$headers = @{\n    "Content-Type" = "application/x-www-form-urlencoded"\n`;
    if (config.authMethod === 'client_secret_basic' && config.clientSecret) {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      script += `    "Authorization" = "Basic ${basic}"\n`;
    }
    script += `}\n\n$response = Invoke-RestMethod -Uri "${tokenUrl}" -Method Post -Headers $headers -Body $body\n$response | ConvertTo-Json -Depth 5`;
    return script;
  };

  const generateCSharp = (): string => {
    let code = `using System.Net.Http.Headers;\n\nvar client = new HttpClient();\nvar request = new HttpRequestMessage(HttpMethod.Post, "${tokenUrl}");\n`;
    if (config.authMethod === 'client_secret_basic' && config.clientSecret) {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      code += `request.Headers.Authorization = new AuthenticationHeaderValue("Basic", "${basic}");\n`;
    }
    code += `\nvar collection = new List<KeyValuePair<string, string>>\n{\n`;
    for (const [k, v] of Object.entries(formData)) {
      code += `    new("${k}", "${v}"),\n`;
    }
    code += `};\n\nvar content = new FormUrlEncodedContent(collection);\nrequest.Content = content;\n\nvar response = await client.SendAsync(request);\nvar responseString = await response.Content.ReadAsStringAsync();\nConsole.WriteLine(responseString);`;
    return code;
  };

  const generateJavaScript = (): string => {
    let script = `const params = new URLSearchParams();\n`;
    for (const [k, v] of Object.entries(formData)) {
      script += `params.append("${k}", "${v}");\n`;
    }
    script += `\nconst headers = {\n  'Content-Type': 'application/x-www-form-urlencoded'\n};\n`;
    if (config.authMethod === 'client_secret_basic' && config.clientSecret) {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      script += `headers['Authorization'] = 'Basic ${basic}';\n`;
    }
    script += `\nconst response = await fetch("${tokenUrl}", {\n  method: 'POST',\n  headers,\n  body: params\n});\nconst data = await response.json();\nconsole.log(data);`;
    return script;
  };

  // Wysłanie zapytania na żywo do lokalnego serwera
  const handleSendLiveRequest = async () => {
    setIsLoading(true);
    const start = performance.now();
    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/x-www-form-urlencoded'
      };

      if (config.authMethod === 'client_secret_basic' && config.clientSecret) {
        headers['Authorization'] = 'Basic ' + btoa(`${config.clientId}:${config.clientSecret}`);
      }

      const res = await fetch(tokenUrl, {
        method: 'POST',
        headers,
        body: urlEncodedBody
      });

      const durationMs = Math.round(performance.now() - start);
      const resHeaders: Record<string, string> = {};
      res.headers.forEach((val, key) => {
        resHeaders[key] = val;
      });

      let jsonBody: Record<string, unknown> = {};
      let rawText = '';
      try {
        rawText = await res.text();
        jsonBody = JSON.parse(rawText);
      } catch {
        jsonBody = { message: rawText || 'Brak treści odpowiedzi' };
      }

      const decodedAccessToken = jsonBody.access_token && typeof jsonBody.access_token === 'string'
        ? parseAndDecodeJwt(jsonBody.access_token)
        : null;

      const decodedIdToken = jsonBody.id_token && typeof jsonBody.id_token === 'string'
        ? parseAndDecodeJwt(jsonBody.id_token)
        : null;

      setResponse({
        status: res.status,
        statusText: res.statusText,
        durationMs,
        headers: resHeaders,
        body: jsonBody,
        rawJson: rawText,
        isError: !res.ok,
        isSimulated: false,
        timestamp: new Date().toLocaleTimeString(),
        decodedAccessToken,
        decodedIdToken
      });
    } catch (err: unknown) {
      const durationMs = Math.round(performance.now() - start);
      const errorMessage = err instanceof Error ? err.message : String(err);
      setResponse({
        status: 0,
        statusText: 'Network Error',
        durationMs,
        headers: {},
        body: {
          error: 'network_error',
          error_description: `Nie udało się połączyć z ${tokenUrl}. Upewnij się, że serwer Quorum.Backend działa lokalnie lub sprawdź certyfikat HTTPS.`,
          details: errorMessage
        },
        isError: true,
        isSimulated: false,
        timestamp: new Date().toLocaleTimeString()
      });
    } finally {
      setIsLoading(false);
    }
  };

  // Symulacja realistycznej odpowiedzi serwera (Mock)
  const handleSimulateMockResponse = () => {
    setIsLoading(true);
    setTimeout(() => {
      const now = Math.floor(Date.now() / 1000);
      const exp = now + 3600;

      // Mock Access Token
      const accessToken = createMockJwt(
        { alg: 'RS256', typ: 'JWT', kid: 'quorum-mock-key-2026' },
        {
          iss: config.issuerUrl,
          sub: 'usr_8f2941b3-4c91-4d32',
          aud: ['quorum.api', 'quorum.gateway'],
          client_id: config.clientId,
          scope: config.scopes.join(' '),
          name: 'Jan Kowalski (Admin)',
          email: 'admin@quorum.local',
          email_verified: true,
          role: ['Admin', 'Manager'],
          nbf: now,
          iat: now,
          exp: exp,
          auth_time: now
        }
      );

      // Mock ID Token
      const idToken = createMockJwt(
        { alg: 'RS256', typ: 'JWT', kid: 'quorum-mock-key-2026' },
        {
          iss: config.issuerUrl,
          sub: 'usr_8f2941b3-4c91-4d32',
          aud: config.clientId,
          nonce: config.nonce,
          name: 'Jan Kowalski',
          preferred_username: 'jkowalski',
          email: 'admin@quorum.local',
          iat: now,
          exp: exp,
          auth_time: now
        }
      );

      const mockBody = {
        access_token: accessToken,
        token_type: 'Bearer',
        expires_in: 3600,
        id_token: idToken,
        refresh_token: config.scopes.includes('offline_access') ? 'rt_' + generateRandomString(40) : undefined,
        scope: config.scopes.join(' ')
      };

      const decodedAccessToken = parseAndDecodeJwt(accessToken);
      const decodedIdToken = parseAndDecodeJwt(idToken);

      setResponse({
        status: 200,
        statusText: 'OK (Mock Simulated)',
        durationMs: 42,
        headers: {
          'content-type': 'application/json; charset=utf-8',
          'cache-control': 'no-store',
          'pragma': 'no-cache',
          'x-content-type-options': 'nosniff',
          'x-powered-by': 'OpenIddict / ASP.NET Core 10'
        },
        body: mockBody,
        rawJson: JSON.stringify(mockBody, null, 2),
        isError: false,
        isSimulated: true,
        timestamp: new Date().toLocaleTimeString(),
        decodedAccessToken,
        decodedIdToken
      });
      setIsLoading(false);
    }, 350);
  };

  const toggleScope = (scopeId: string) => {
    if (config.scopes.includes(scopeId)) {
      onChangeConfig({ scopes: config.scopes.filter((s) => s !== scopeId) });
    } else {
      onChangeConfig({ scopes: [...config.scopes, scopeId] });
    }
  };

  const addCustomScope = () => {
    if (customScope.trim() && !config.scopes.includes(customScope.trim())) {
      onChangeConfig({ scopes: [...config.scopes, customScope.trim()] });
      setCustomScope('');
    }
  };

  const handleCopyAuthorizeUrl = () => {
    navigator.clipboard.writeText(authorizeUrl);
    setCopiedUrl(true);
    setTimeout(() => setCopiedUrl(false), 2000);
  };

  return (
    <div className="flex flex-col gap-8">
      {/* Step 1: Authorization Request (GET /connect/authorize) */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 flex flex-col gap-5 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-blue-600/20 text-blue-400 border border-blue-500/30 flex items-center justify-center font-bold text-sm">
              1
            </div>
            <div>
              <h3 className="text-base font-bold text-white flex items-center gap-2">
                Krok 1: Żądanie Autoryzacji (GET /connect/authorize)
              </h3>
              <p className="text-xs text-slate-400">
                Użytkownik zostaje przekierowany do strony logowania Open.IdentityServer z parametrami PKCE i Client ID.
              </p>
            </div>
          </div>

          <button
            onClick={handleRegenerateCrypto}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg border border-slate-700 transition-colors cursor-pointer self-start sm:self-auto"
            title="Generuje nową parę PKCE verifier/challenge oraz losowe State i Nonce"
          >
            <RefreshCw className="w-3.5 h-3.5 text-blue-400" />
            <span>Nowe PKCE & State</span>
          </button>
        </div>

        {/* Input Parameters Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Client ID
            </label>
            <input
              type="text"
              value={config.clientId}
              onChange={(e) => onChangeConfig({ clientId: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none"
              placeholder="frontend-spa-portal"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Redirect URI
            </label>
            <input
              type="text"
              value={config.redirectUri}
              onChange={(e) => onChangeConfig({ redirectUri: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none"
              placeholder="http://localhost:3000/callback"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Response Type
            </label>
            <input
              type="text"
              value={config.responseType}
              onChange={(e) => onChangeConfig({ responseType: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none"
              placeholder="code"
            />
          </div>

          {/* PKCE Challenge Details */}
          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="text-xs font-semibold text-slate-300 flex items-center gap-1">
                <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
                PKCE code_challenge (S256)
              </label>
              <span className="text-[10px] text-slate-500 font-mono">RFC 7636</span>
            </div>
            <input
              type="text"
              readOnly
              value={config.codeChallenge}
              className="w-full bg-slate-950/80 border border-slate-800 rounded-lg px-3 py-2 text-[11px] font-mono text-emerald-300 truncate outline-none cursor-text"
              title={config.codeChallenge}
            />
          </div>

          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="text-xs font-semibold text-slate-300">State (Ochrona CSRF)</label>
              <span className="text-[10px] text-slate-500 font-mono">Losowy ciąg</span>
            </div>
            <input
              type="text"
              value={config.state}
              onChange={(e) => onChangeConfig({ state: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none"
            />
          </div>

          <div>
            <div className="flex justify-between items-center mb-1">
              <label className="text-xs font-semibold text-slate-300">Nonce (Dla ID Token)</label>
              <span className="text-[10px] text-slate-500 font-mono">OIDC Nonce</span>
            </div>
            <input
              type="text"
              value={config.nonce}
              onChange={(e) => onChangeConfig({ nonce: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none"
            />
          </div>
        </div>

        {/* Scope Selector Chips */}
        <div className="flex flex-col gap-2">
          <label className="text-xs font-semibold text-slate-300 flex items-center justify-between">
            <span>Wybrane Zakresy (Scopes):</span>
            <span className="text-[11px] font-normal text-slate-400">
              Zaznacz lub odznacz pożądane zakresy OIDC
            </span>
          </label>
          <div className="flex flex-wrap gap-2">
            {AVAILABLE_SCOPES.map((sc) => {
              const isSelected = config.scopes.includes(sc.id);
              return (
                <button
                  key={sc.id}
                  onClick={() => toggleScope(sc.id)}
                  className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-mono transition-colors cursor-pointer ${
                    isSelected
                      ? 'bg-blue-600/20 text-blue-300 border border-blue-500/40 font-semibold'
                      : 'bg-slate-950 text-slate-400 border border-slate-800 hover:text-slate-200 hover:bg-slate-800'
                  }`}
                  title={sc.desc}
                >
                  {isSelected ? (
                    <CheckSquare className="w-3.5 h-3.5 text-blue-400" />
                  ) : (
                    <Square className="w-3.5 h-3.5 text-slate-600" />
                  )}
                  <span>{sc.label}</span>
                </button>
              );
            })}
          </div>

          {/* Custom Scope input */}
          <div className="flex items-center gap-2 mt-1">
            <input
              type="text"
              value={customScope}
              onChange={(e) => setCustomScope(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && addCustomScope()}
              placeholder="Dodaj niestandardowy scope (np. my.custom.api)..."
              className="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 w-72 outline-none focus:border-blue-500"
            />
            <button
              onClick={addCustomScope}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-medium cursor-pointer border border-slate-700"
            >
              + Dodaj
            </button>
          </div>
        </div>

        {/* Generated Authorization URL Preview Box */}
        <div className="bg-slate-950 rounded-xl p-4 border border-slate-800 flex flex-col gap-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-300">
              <span className="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-mono text-[10px]">
                GET URL
              </span>
              Wygenerowany adres logowania autoryzacyjnego
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={handleCopyAuthorizeUrl}
                className="flex items-center gap-1.5 px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-md text-xs transition-colors cursor-pointer border border-slate-700"
              >
                {copiedUrl ? (
                  <>
                    <Check className="w-3.5 h-3.5 text-emerald-400" />
                    <span className="text-emerald-400">Skopiowano</span>
                  </>
                ) : (
                  <>
                    <Copy className="w-3.5 h-3.5" />
                    <span>Kopiuj URL</span>
                  </>
                )}
              </button>

              <a
                href={authorizeUrl}
                target="_blank"
                rel="noreferrer"
                className="flex items-center gap-1.5 px-3 py-1 bg-blue-600 hover:bg-blue-500 text-white rounded-md text-xs font-medium transition-colors cursor-pointer"
              >
                <ExternalLink className="w-3.5 h-3.5" />
                <span>Otwórz w Nowej Karcie</span>
              </a>
            </div>
          </div>

          <div className="bg-slate-900 p-3 rounded-lg border border-slate-800/80 font-mono text-[11px] text-blue-300 break-all leading-relaxed select-all">
            {authorizeUrl}
          </div>
        </div>
      </div>

      {/* Step 2: Token Exchange Request (POST /connect/token) */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 flex flex-col gap-5 shadow-sm">
        <div className="flex items-center gap-3 pb-3 border-b border-slate-800">
          <div className="w-8 h-8 rounded-lg bg-emerald-600/20 text-emerald-400 border border-emerald-500/30 flex items-center justify-center font-bold text-sm">
            2
          </div>
          <div>
            <h3 className="text-base font-bold text-white flex items-center gap-2">
              Krok 2: Wymiana Kodu na Tokeny (POST /connect/token)
            </h3>
            <p className="text-xs text-slate-400">
              Klient wysyła uzyskany kod autoryzacyjny wraz z <code className="text-emerald-300">code_verifier</code> (PKCE) do endpointu tokenów, otrzymując Access Token, ID Token i Refresh Token.
            </p>
          </div>
        </div>

        {/* Code exchange parameters */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="md:col-span-1">
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Metoda Uwierzytelnienia Klienta
            </label>
            <select
              value={config.authMethod}
              onChange={(e) => onChangeConfig({ authMethod: e.target.value as any })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:ring-2 focus:ring-blue-500 outline-none"
            >
              <option value="none">Brak sekretu (Publiczny klient SPA + PKCE)</option>
              <option value="client_secret_post">client_secret_post (Sekret w ciele POST)</option>
              <option value="client_secret_basic">client_secret_basic (Nagłówek HTTP Basic)</option>
            </select>
          </div>

          {config.authMethod !== 'none' && (
            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-1">
                Client Secret
              </label>
              <input
                type="password"
                value={config.clientSecret}
                onChange={(e) => onChangeConfig({ clientSecret: e.target.value })}
                className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-blue-500 outline-none"
                placeholder="tajny_sekret_klienta"
              />
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Zwrócony Authorization Code
            </label>
            <input
              type="text"
              value={config.authCode}
              onChange={(e) => onChangeConfig({ authCode: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-amber-300 focus:ring-2 focus:ring-blue-500 outline-none"
              placeholder="wpisz lub wklej otrzymany kod..."
            />
          </div>

          <div className="md:col-span-3">
            <div className="flex justify-between items-center mb-1">
              <label className="text-xs font-semibold text-slate-300 flex items-center gap-1">
                <Key className="w-3.5 h-3.5 text-blue-400" />
                PKCE code_verifier (Wysyłany w ciele zapytania)
              </label>
              <span className="text-[10px] text-slate-500 font-mono">Musi pasować do S256(challenge)</span>
            </div>
            <input
              type="text"
              readOnly
              value={config.codeVerifier}
              className="w-full bg-slate-950/80 border border-slate-800 rounded-lg px-3 py-2 text-[11px] font-mono text-slate-300 outline-none"
            />
          </div>
        </div>

        {/* Request Code Viewer (cURL, Raw HTTP, PowerShell, C#) */}
        <div>
          <div className="text-xs font-semibold text-slate-300 mb-2">
            Podgląd żądania POST /connect/token w różnych formatach:
          </div>
          <RequestCodeViewer
            httpRaw={generateRawHttp()}
            curl={generateCurl()}
            powershell={generatePowerShell()}
            csharp={generateCSharp()}
            javascript={generateJavaScript()}
            urlPreview={tokenUrl}
            method="POST"
          />
        </div>

        {/* Action Buttons: Live vs Simulate */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-800">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <span className="w-2 h-2 rounded-full bg-emerald-400" />
            Endpoint docelowy: <code className="text-slate-200 font-mono">{tokenUrl}</code>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={handleSimulateMockResponse}
              disabled={isLoading}
              className="flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium rounded-xl border border-slate-700 transition-all cursor-pointer disabled:opacity-50"
            >
              <Sparkles className="w-4 h-4 text-purple-400" />
              <span>Symuluj odpowiedź serwera (Mock)</span>
            </button>

            <button
              onClick={handleSendLiveRequest}
              disabled={isLoading}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium rounded-xl shadow-md hover:shadow-blue-600/20 transition-all cursor-pointer disabled:opacity-50"
            >
              <Send className="w-4 h-4" />
              <span>Wyślij zapytanie na żywo do serwera</span>
            </button>
          </div>
        </div>
      </div>

      {/* Response Inspector Section */}
      <ResponseInspector
        response={response}
        isLoading={isLoading}
        onClear={() => setResponse(null)}
      />
    </div>
  );
};
