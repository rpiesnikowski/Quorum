import React, { useState } from 'react';
import { 
  Send, 
  Sparkles, 
  Compass, 
  ExternalLink, 
  CheckCircle2, 
  FileCode, 
  ShieldCheck, 
  Key, 
  Layers
} from 'lucide-react';
import { DiscoveryConfig, OidcResponse } from '../../types/oidc';
import { RequestCodeViewer } from './RequestCodeViewer';
import { ResponseInspector } from './ResponseInspector';

interface DiscoveryFlowViewProps {
  config: DiscoveryConfig;
  onChangeConfig: (updated: Partial<DiscoveryConfig>) => void;
}

export const DiscoveryFlowView: React.FC<DiscoveryFlowViewProps> = ({
  config,
  onChangeConfig
}) => {
  const [isLoading, setIsLoading] = useState(false);
  const [response, setResponse] = useState<OidcResponse | null>(null);

  const discoveryUrl = config.issuerUrl.replace(/\/+$/, '') + (config.discoveryPath.startsWith('/') ? config.discoveryPath : '/' + config.discoveryPath);

  const generateRawHttp = (): string => {
    const parsedUrl = new URL(discoveryUrl);
    return `GET ${parsedUrl.pathname} HTTP/1.1\nHost: ${parsedUrl.host}\nAccept: application/json`;
  };

  const generateCurl = (): string => {
    return `curl -X GET "${discoveryUrl}" \\\n  -H "Accept: application/json"`;
  };

  const generatePowerShell = (): string => {
    return `$response = Invoke-RestMethod -Uri "${discoveryUrl}" -Method Get\n$response | ConvertTo-Json -Depth 6`;
  };

  const generateCSharp = (): string => {
    return `var client = new HttpClient();\nvar response = await client.GetAsync("${discoveryUrl}");\nvar json = await response.Content.ReadAsStringAsync();\nConsole.WriteLine(json);`;
  };

  const generateJavaScript = (): string => {
    return `const res = await fetch("${discoveryUrl}");\nconst metadata = await res.json();\nconsole.log(metadata);`;
  };

  const handleSendLiveRequest = async () => {
    setIsLoading(true);
    const start = performance.now();
    try {
      const res = await fetch(discoveryUrl, {
        method: 'GET',
        headers: { Accept: 'application/json' }
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

      setResponse({
        status: res.status,
        statusText: res.statusText,
        durationMs,
        headers: resHeaders,
        body: jsonBody,
        rawJson: rawText,
        isError: !res.ok,
        isSimulated: false,
        timestamp: new Date().toLocaleTimeString()
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
          error_description: `Nie można pobrać dokumentu Discovery z ${discoveryUrl}. Sprawdź, czy Quorum.Backend jest uruchomiony na porcie 5000/5001.`,
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

  const handleSimulateMockResponse = () => {
    setIsLoading(true);
    setTimeout(() => {
      const base = config.issuerUrl.replace(/\/+$/, '');
      const mockDiscovery = {
        issuer: base,
        authorization_endpoint: `${base}/connect/authorize`,
        token_endpoint: `${base}/connect/token`,
        userinfo_endpoint: `${base}/connect/userinfo`,
        end_session_endpoint: `${base}/connect/endsession`,
        jwks_uri: `${base}/.well-known/openid-configuration/jwks`,
        introspection_endpoint: `${base}/connect/introspect`,
        revocation_endpoint: `${base}/connect/revocation`,
        response_types_supported: ['code', 'token', 'id_token', 'code id_token'],
        response_modes_supported: ['query', 'form_post', 'fragment'],
        grant_types_supported: [
          'authorization_code',
          'client_credentials',
          'refresh_token',
          'implicit'
        ],
        subject_types_supported: ['public'],
        id_token_signing_alg_values_supported: ['RS256'],
        code_challenge_methods_supported: ['S256', 'plain'],
        scopes_supported: [
          'openid',
          'profile',
          'email',
          'quorum.api',
          'quorum.gateway',
          'telemetry.read',
          'offline_access'
        ],
        token_endpoint_auth_methods_supported: [
          'client_secret_post',
          'client_secret_basic',
          'none'
        ],
        claims_supported: [
          'sub',
          'name',
          'given_name',
          'family_name',
          'preferred_username',
          'email',
          'email_verified',
          'role',
          'aud',
          'iss'
        ]
      };

      setResponse({
        status: 200,
        statusText: 'OK (Mock Discovery)',
        durationMs: 25,
        headers: {
          'content-type': 'application/json; charset=utf-8',
          'cache-control': 'max-age=3600',
          'x-powered-by': 'OpenIddict / ASP.NET Core 10'
        },
        body: mockDiscovery,
        rawJson: JSON.stringify(mockDiscovery, null, 2),
        isError: false,
        isSimulated: true,
        timestamp: new Date().toLocaleTimeString()
      });
      setIsLoading(false);
    }, 250);
  };

  return (
    <div className="flex flex-col gap-8">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 flex flex-col gap-5 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-slate-800">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-teal-600/20 text-teal-400 border border-teal-500/30 flex items-center justify-center font-bold text-sm">
              <Compass className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-base font-bold text-white flex items-center gap-2">
                OpenID Connect Discovery Document
              </h3>
              <p className="text-xs text-slate-400">
                Weryfikacja opublikowanej konfiguracji dostawcy tożsamości (RFC 8414 & OpenID Connect Discovery 1.0).
              </p>
            </div>
          </div>

          <a
            href={discoveryUrl}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-medium transition-colors cursor-pointer border border-slate-700"
          >
            <ExternalLink className="w-3.5 h-3.5" />
            <span>Otwórz w przeglądarce</span>
          </a>
        </div>

        {/* Path Input */}
        <div>
          <label className="block text-xs font-semibold text-slate-300 mb-1">
            Ścieżka dokumentu Discovery
          </label>
          <input
            type="text"
            value={config.discoveryPath}
            onChange={(e) => onChangeConfig({ discoveryPath: e.target.value })}
            className="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs font-mono text-white focus:ring-2 focus:ring-teal-500 outline-none"
          />
        </div>

        {/* Code Snippets */}
        <div>
          <div className="text-xs font-semibold text-slate-300 mb-2">
            Pobieranie dokumentu konfiguracyjnego w różnych narzędziach:
          </div>
          <RequestCodeViewer
            httpRaw={generateRawHttp()}
            curl={generateCurl()}
            powershell={generatePowerShell()}
            csharp={generateCSharp()}
            javascript={generateJavaScript()}
            urlPreview={discoveryUrl}
            method="GET"
          />
        </div>

        {/* Actions */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-800">
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <span className="w-2 h-2 rounded-full bg-teal-400" />
            Adres: <code className="text-slate-200 font-mono">{discoveryUrl}</code>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={handleSimulateMockResponse}
              disabled={isLoading}
              className="flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium rounded-xl border border-slate-700 transition-all cursor-pointer disabled:opacity-50"
            >
              <Sparkles className="w-4 h-4 text-purple-400" />
              <span>Symuluj dokument (Mock)</span>
            </button>

            <button
              onClick={handleSendLiveRequest}
              disabled={isLoading}
              className="flex items-center gap-2 px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white text-xs font-medium rounded-xl shadow-md hover:shadow-teal-600/20 transition-all cursor-pointer disabled:opacity-50"
            >
              <Send className="w-4 h-4" />
              <span>Pobierz z serwera na żywo</span>
            </button>
          </div>
        </div>
      </div>

      <ResponseInspector
        response={response}
        isLoading={isLoading}
        onClear={() => setResponse(null)}
      />
    </div>
  );
};
