import React, { useState } from 'react';
import { 
  Send, 
  Sparkles, 
  Key, 
  Server, 
  ShieldCheck, 
  CheckSquare, 
  Square,
  Lock
} from 'lucide-react';
import { ClientCredentialsConfig, OidcResponse, AVAILABLE_SCOPES } from '../../types/oidc';
import { RequestCodeViewer } from './RequestCodeViewer';
import { ResponseInspector } from './ResponseInspector';
import { parseAndDecodeJwt, createMockJwt } from '../../utils/oidcCrypto';

interface ClientCredentialsFlowViewProps {
  config: ClientCredentialsConfig;
  onChangeConfig: (updated: Partial<ClientCredentialsConfig>) => void;
}

export const ClientCredentialsFlowView: React.FC<ClientCredentialsFlowViewProps> = ({
  config,
  onChangeConfig
}) => {
  const [isLoading, setIsLoading] = useState(false);
  const [response, setResponse] = useState<OidcResponse | null>(null);
  const [customScope, setCustomScope] = useState('');

  const tokenUrl = config.issuerUrl.replace(/\/+$/, '') + (config.tokenEndpoint.startsWith('/') ? config.tokenEndpoint : '/' + config.tokenEndpoint);

  const getFormData = (): Record<string, string> => {
    const data: Record<string, string> = {
      grant_type: 'client_credentials',
      scope: config.scopes.join(' ')
    };

    if (config.authMethod === 'client_secret_post') {
      data.client_id = config.clientId;
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

    if (config.authMethod === 'client_secret_basic') {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      headers += `\nAuthorization: Basic ${basic}`;
    }

    return `${headers}\n\n${urlEncodedBody}`;
  };

  const generateCurl = (): string => {
    let cmd = `curl -X POST "${tokenUrl}" \\\n  -H "Content-Type: application/x-www-form-urlencoded"`;
    if (config.authMethod === 'client_secret_basic') {
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
    if (config.authMethod === 'client_secret_basic') {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      script += `    "Authorization" = "Basic ${basic}"\n`;
    }
    script += `}\n\n$response = Invoke-RestMethod -Uri "${tokenUrl}" -Method Post -Headers $headers -Body $body\n$response | ConvertTo-Json -Depth 5`;
    return script;
  };

  const generateCSharp = (): string => {
    let code = `using System.Net.Http.Headers;\n\nvar client = new HttpClient();\nvar request = new HttpRequestMessage(HttpMethod.Post, "${tokenUrl}");\n`;
    if (config.authMethod === 'client_secret_basic') {
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
    if (config.authMethod === 'client_secret_basic') {
      const basic = btoa(`${config.clientId}:${config.clientSecret}`);
      script += `headers['Authorization'] = 'Basic ${basic}';\n`;
    }
    script += `\nconst response = await fetch("${tokenUrl}", {\n  method: 'POST',\n  headers,\n  body: params\n});\nconst data = await response.json();\nconsole.log(data);`;
    return script;
  };

  // Wysyłanie żądania na żywo do serwera
  const handleSendLiveRequest = async () => {
    setIsLoading(true);
    const start = performance.now();
    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/x-www-form-urlencoded'
      };

      if (config.authMethod === 'client_secret_basic') {
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
        decodedAccessToken
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
          error_description: `Nie udało się połączyć z ${tokenUrl}. Sprawdź, czy serwis Quorum.Backend jest uruchomiony na porcie 5000/5001.`,
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

      // Mock M2M Token
      const accessToken = createMockJwt(
        { alg: 'RS256', typ: 'JWT', kid: 'quorum-m2m-key-2026' },
        {
          iss: config.issuerUrl,
          sub: config.clientId,
          aud: ['quorum.api', 'quorum.gateway'],
          client_id: config.clientId,
          scope: config.scopes.join(' '),
          token_use: 'access_token',
          is_m2m: true,
          nbf: now,
          iat: now,
          exp: exp
        }
      );

      const mockBody = {
        access_token: accessToken,
        token_type: 'Bearer',
        expires_in: 3600,
        scope: config.scopes.join(' ')
      };

      const decodedAccessToken = parseAndDecodeJwt(accessToken);

      setResponse({
        status: 200,
        statusText: 'OK (Mock Simulated)',
        durationMs: 38,
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
        decodedAccessToken
      });
      setIsLoading(false);
    }, 300);
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

  return (
    <div className="flex flex-col gap-8">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 flex flex-col gap-5 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-purple-600/20 text-purple-400 border border-purple-500/30 flex items-center justify-center font-bold text-sm">
              <Server className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-base font-bold text-white flex items-center gap-2">
                Client Credentials Flow (Machine to Machine)
              </h3>
              <p className="text-xs text-slate-400">
                Wymiana poświadczeń klienta (Client ID + Client Secret) bezpośrednio na Access Token bez udziału użytkownika końcowego.
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="px-2.5 py-1 rounded-full text-xs font-mono font-medium bg-purple-500/10 text-purple-300 border border-purple-500/20">
              grant_type=client_credentials
            </span>
          </div>
        </div>

        {/* Input Parameters Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Client ID
            </label>
            <input
              type="text"
              value={config.clientId}
              onChange={(e) => onChangeConfig({ clientId: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-purple-500 outline-none"
              placeholder="backend-worker-service"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Client Secret
            </label>
            <input
              type="password"
              value={config.clientSecret}
              onChange={(e) => onChangeConfig({ clientSecret: e.target.value })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-purple-500 outline-none"
              placeholder="Pass123$"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-1">
              Metoda Uwierzytelnienia Klienta
            </label>
            <select
              value={config.authMethod}
              onChange={(e) => onChangeConfig({ authMethod: e.target.value as any })}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:ring-2 focus:ring-purple-500 outline-none"
            >
              <option value="client_secret_post">client_secret_post (Sekret w ciele POST)</option>
              <option value="client_secret_basic">client_secret_basic (Nagłówek HTTP Basic)</option>
            </select>
          </div>
        </div>

        {/* Scopes Section */}
        <div className="flex flex-col gap-2">
          <label className="text-xs font-semibold text-slate-300 flex items-center justify-between">
            <span>Żądane Zakresy (Scopes):</span>
            <span className="text-[11px] font-normal text-slate-400">
              Wybierz zakresy uprawnień dla serwisu backendowego
            </span>
          </label>
          <div className="flex flex-wrap gap-2">
            {AVAILABLE_SCOPES.filter((s) => s.id !== 'openid' && s.id !== 'profile' && s.id !== 'email').map((sc) => {
              const isSelected = config.scopes.includes(sc.id);
              return (
                <button
                  key={sc.id}
                  onClick={() => toggleScope(sc.id)}
                  className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-mono transition-colors cursor-pointer ${
                    isSelected
                      ? 'bg-purple-600/20 text-purple-300 border border-purple-500/40 font-semibold'
                      : 'bg-slate-950 text-slate-400 border border-slate-800 hover:text-slate-200 hover:bg-slate-800'
                  }`}
                  title={sc.desc}
                >
                  {isSelected ? (
                    <CheckSquare className="w-3.5 h-3.5 text-purple-400" />
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
              placeholder="Dodaj niestandardowy scope (np. telemetry.write)..."
              className="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 w-72 outline-none focus:border-purple-500"
            />
            <button
              onClick={addCustomScope}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-medium cursor-pointer border border-slate-700"
            >
              + Dodaj
            </button>
          </div>
        </div>

        {/* Code Snippets Viewer */}
        <div>
          <div className="text-xs font-semibold text-slate-300 mb-2">
            Wygenerowane żądanie tokenu M2M w różnych środowiskach:
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

        {/* Action Buttons */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-800">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <span className="w-2 h-2 rounded-full bg-purple-400" />
            Endpoint: <code className="text-slate-200 font-mono">{tokenUrl}</code>
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
              className="flex items-center gap-2 px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white text-xs font-medium rounded-xl shadow-md hover:shadow-purple-600/20 transition-all cursor-pointer disabled:opacity-50"
            >
              <Send className="w-4 h-4" />
              <span>Wyślij zapytanie na żywo do serwera</span>
            </button>
          </div>
        </div>
      </div>

      {/* Response Inspector */}
      <ResponseInspector
        response={response}
        isLoading={isLoading}
        onClear={() => setResponse(null)}
      />
    </div>
  );
};
