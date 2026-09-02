import React, { useState } from 'react';
import { 
  CheckCircle2, 
  AlertTriangle, 
  XCircle, 
  Clock, 
  Copy, 
  Check, 
  KeyRound, 
  Info, 
  ShieldAlert, 
  FileJson, 
  ListTree
} from 'lucide-react';
import { OidcResponse, DecodedJwt } from '../../types/oidc';
import { formatJwtExpiry } from '../../utils/oidcCrypto';

interface ResponseInspectorProps {
  response: OidcResponse | null;
  isLoading: boolean;
  onClear?: () => void;
}

export const ResponseInspector: React.FC<ResponseInspectorProps> = ({
  response,
  isLoading,
  onClear
}) => {
  const [activeTab, setActiveTab] = useState<'json' | 'jwt' | 'headers'>('json');
  const [copiedField, setCopiedField] = useState<string | null>(null);

  if (isLoading) {
    return (
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-8 flex flex-col items-center justify-center gap-3 text-center min-h-[320px]">
        <div className="w-10 h-10 border-3 border-blue-500 border-t-transparent rounded-full animate-spin" />
        <div className="font-semibold text-white text-sm">Wysyłanie zapytania OIDC...</div>
        <p className="text-xs text-slate-400 max-w-sm">
          Nawiązywanie połączenia z lokalnym endpointem serwera tożsamości i weryfikacja nagłówków.
        </p>
      </div>
    );
  }

  if (!response) {
    return (
      <div className="bg-slate-900/60 border border-dashed border-slate-800 rounded-2xl p-8 flex flex-col items-center justify-center gap-3 text-center min-h-[280px]">
        <div className="w-12 h-12 rounded-xl bg-slate-800 text-slate-500 flex items-center justify-center">
          <KeyRound className="w-6 h-6" />
        </div>
        <div className="font-semibold text-slate-300 text-sm">Oczekiwanie na wykonanie zapytania</div>
        <p className="text-xs text-slate-500 max-w-md">
          Użyj przycisku <strong>„Wyślij zapytanie na żywo”</strong> do swojego lokalnego serwera ASP.NET Core lub przetestuj konfigurację za pomocą przycisku <strong>„Symuluj odpowiedź serwera”</strong>.
        </p>
      </div>
    );
  }

  const handleCopyText = (text: string, fieldName: string) => {
    navigator.clipboard.writeText(text);
    setCopiedField(fieldName);
    setTimeout(() => setCopiedField(null), 2000);
  };

  const getStatusBadge = () => {
    if (response.status >= 200 && response.status < 300) {
      return (
        <span className="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-500/15 text-emerald-400 border border-emerald-500/30">
          <CheckCircle2 className="w-3.5 h-3.5" />
          {response.status} {response.statusText || 'OK'}
        </span>
      );
    }
    if (response.status === 0) {
      return (
        <span className="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-500/15 text-amber-400 border border-amber-500/30">
          <AlertTriangle className="w-3.5 h-3.5" />
          Błąd połączenia / CORS
        </span>
      );
    }
    return (
      <span className="flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-rose-500/15 text-rose-400 border border-rose-500/30">
        <XCircle className="w-3.5 h-3.5" />
        {response.status} {response.statusText || 'Error'}
      </span>
    );
  };

  const hasJwt = Boolean(response.decodedAccessToken || response.decodedIdToken);

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden flex flex-col shadow-sm">
      {/* Response Header Status Bar */}
      <div className="p-4 bg-slate-900 border-b border-slate-800 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          {getStatusBadge()}
          <span className="flex items-center gap-1 text-xs text-slate-400">
            <Clock className="w-3.5 h-3.5 text-slate-500" />
            {response.durationMs} ms
          </span>
          {response.isSimulated ? (
            <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-purple-500/15 text-purple-300 border border-purple-500/30">
              Symulacja Mock
            </span>
          ) : (
            <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-blue-500/15 text-blue-300 border border-blue-500/30">
              Lokalny Serwer Live
            </span>
          )}
        </div>

        <div className="flex items-center gap-2">
          {onClear && (
            <button
              onClick={onClear}
              className="text-xs text-slate-500 hover:text-slate-300 transition-colors cursor-pointer px-2 py-1"
            >
              Wyczyść
            </button>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-800 px-4 pt-2 bg-slate-950/40 gap-2">
        <button
          onClick={() => setActiveTab('json')}
          className={`px-3 py-2 text-xs font-medium border-b-2 transition-colors cursor-pointer flex items-center gap-1.5 ${
            activeTab === 'json'
              ? 'border-blue-500 text-blue-400'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <FileJson className="w-3.5 h-3.5" />
          Odpowiedź JSON
        </button>

        {hasJwt && (
          <button
            onClick={() => setActiveTab('jwt')}
            className={`px-3 py-2 text-xs font-medium border-b-2 transition-colors cursor-pointer flex items-center gap-1.5 ${
              activeTab === 'jwt'
                ? 'border-blue-500 text-blue-400'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            <KeyRound className="w-3.5 h-3.5 text-emerald-400" />
            Dekoder Tokenu JWT
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          </button>
        )}

        <button
          onClick={() => setActiveTab('headers')}
          className={`px-3 py-2 text-xs font-medium border-b-2 transition-colors cursor-pointer flex items-center gap-1.5 ${
            activeTab === 'headers'
              ? 'border-blue-500 text-blue-400'
              : 'border-transparent text-slate-400 hover:text-slate-200'
          }`}
        >
          <ListTree className="w-3.5 h-3.5" />
          Nagłówki HTTP ({Object.keys(response.headers).length})
        </button>
      </div>

      {/* Network Error / CORS Helper */}
      {response.status === 0 && (
        <div className="p-4 m-4 bg-amber-500/10 border border-amber-500/30 rounded-xl text-xs text-amber-200 flex flex-col gap-2">
          <div className="flex items-center gap-2 font-semibold text-amber-300">
            <ShieldAlert className="w-4 h-4 shrink-0" />
            Brak bezpośredniej łączności z przeglądarki (CORS / Serwer offline)
          </div>
          <p className="text-slate-300 leading-relaxed">
            Przeglądarka zablokowała żądanie bezpośrednie do lokalnego serwera. Typowe przyczyny i rozwiązania:
          </p>
          <ul className="list-disc pl-5 space-y-1 text-slate-300">
            <li>
              <strong>Czy lokalny serwer jest włączony?</strong> Uruchom projekt komendą: <code className="text-amber-300">dotnet run --project Quorum.Backend</code>
            </li>
            <li>
              <strong>Brak nagłówków CORS:</strong> Żądania OIDC z innych domen wymagają włączonego CORS w pliku <code className="text-amber-300">Program.cs</code>.
            </li>
            <li>
              <strong>Samopodpisany certyfikat SSL (HTTPS):</strong> Jeśli używasz <code className="text-amber-300">https://localhost:5001</code>, otwórz ten adres bezpośrednio w nowej karcie i zaakceptuj certyfikat deweloperski, lub użyj profilu HTTP <code className="text-amber-300">http://localhost:5000</code>.
            </li>
            <li>
              <strong>Wypróbuj cURL / Terminal:</strong> Skopiuj przygotowane polecenie cURL z zakładki obok i uruchom je bezpośrednio w konsoli systemowej bez ograniczeń przeglądarki!
            </li>
          </ul>
        </div>
      )}

      {/* Content: JSON Tab */}
      {activeTab === 'json' && (
        <div className="p-4 flex flex-col gap-3">
          <div className="flex justify-between items-center">
            <span className="text-xs text-slate-400 font-mono">Payload odpowiedzi serwera</span>
            <button
              onClick={() => handleCopyText(response.rawJson || JSON.stringify(response.body, null, 2), 'body')}
              className="flex items-center gap-1.5 px-2 py-1 text-xs text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 rounded-md transition-colors cursor-pointer border border-slate-700"
            >
              {copiedField === 'body' ? (
                <>
                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                  <span className="text-emerald-400">Skopiowano JSON</span>
                </>
              ) : (
                <>
                  <Copy className="w-3.5 h-3.5" />
                  <span>Kopiuj JSON</span>
                </>
              )}
            </button>
          </div>

          <pre className="bg-slate-950 p-4 rounded-xl border border-slate-800 font-mono text-xs text-emerald-300 overflow-x-auto max-h-[400px] leading-relaxed whitespace-pre">
            {response.rawJson || JSON.stringify(response.body, null, 2)}
          </pre>
        </div>
      )}

      {/* Content: JWT Decoder Tab */}
      {activeTab === 'jwt' && (
        <div className="p-4 flex flex-col gap-6">
          {response.decodedAccessToken && (
            <JwtCard title="Access Token (Poświadczenie dostępu API)" jwt={response.decodedAccessToken} />
          )}

          {response.decodedIdToken && (
            <JwtCard title="ID Token (Identyfikacja OIDC)" jwt={response.decodedIdToken} />
          )}
        </div>
      )}

      {/* Content: Headers Tab */}
      {activeTab === 'headers' && (
        <div className="p-4">
          <div className="border border-slate-800 rounded-xl overflow-hidden">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-950 text-slate-400 font-mono border-b border-slate-800">
                <tr>
                  <th className="p-2.5">Nagłówek HTTP</th>
                  <th className="p-2.5">Wartość</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 font-mono">
                {Object.keys(response.headers).length === 0 ? (
                  <tr>
                    <td colSpan={2} className="p-4 text-center text-slate-500 italic">
                      Brak nagłówków do wyświetlenia
                    </td>
                  </tr>
                ) : (
                  Object.entries(response.headers).map(([key, val]) => (
                    <tr key={key} className="hover:bg-slate-800/40 transition-colors">
                      <td className="p-2.5 text-blue-400 font-medium">{key}</td>
                      <td className="p-2.5 text-slate-300 break-all">{val}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};

interface JwtCardProps {
  title: string;
  jwt: DecodedJwt;
}

const JwtCard: React.FC<JwtCardProps> = ({ title, jwt }) => {
  const [copied, setCopied] = useState(false);

  const handleCopyRaw = () => {
    navigator.clipboard.writeText(jwt.raw);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const expValue = typeof jwt.payload.exp === 'number' ? jwt.payload.exp : undefined;
  const expiryText = formatJwtExpiry(expValue);

  return (
    <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-800 pb-3">
        <div className="flex items-center gap-2">
          <KeyRound className="w-4 h-4 text-emerald-400" />
          <h4 className="font-bold text-sm text-white">{title}</h4>
        </div>
        <div className="flex items-center gap-2">
          {jwt.isExpired ? (
            <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-rose-500/15 text-rose-400 border border-rose-500/30">
              Wygasły
            </span>
          ) : expValue ? (
            <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/15 text-emerald-400 border border-emerald-500/30">
              Aktywny
            </span>
          ) : null}
          <button
            onClick={handleCopyRaw}
            className="flex items-center gap-1 px-2 py-1 text-xs text-slate-300 hover:text-white bg-slate-900 hover:bg-slate-800 rounded border border-slate-800 transition-colors cursor-pointer"
          >
            {copied ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3" />}
            <span>Kopiuj Token</span>
          </button>
        </div>
      </div>

      {/* Expiration Banner */}
      <div className="bg-slate-900/80 px-3 py-2 rounded-lg border border-slate-800 flex items-center justify-between text-xs">
        <span className="text-slate-400 flex items-center gap-1.5">
          <Clock className="w-3.5 h-3.5 text-blue-400" /> Ważność:
        </span>
        <span className="font-mono text-slate-200 font-medium">{expiryText}</span>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Header Decoded */}
        <div className="flex flex-col gap-1.5">
          <div className="text-[11px] font-bold text-rose-400 uppercase tracking-wider">
            Header (Algorytm i Typ)
          </div>
          <pre className="bg-slate-900 p-3 rounded-lg border border-slate-800 font-mono text-xs text-rose-300 overflow-x-auto">
            {JSON.stringify(jwt.header, null, 2)}
          </pre>
        </div>

        {/* Payload Decoded */}
        <div className="flex flex-col gap-1.5">
          <div className="text-[11px] font-bold text-purple-400 uppercase tracking-wider">
            Claims / Roszczenia (Payload)
          </div>
          <pre className="bg-slate-900 p-3 rounded-lg border border-slate-800 font-mono text-xs text-purple-300 overflow-x-auto max-h-[220px]">
            {JSON.stringify(jwt.payload, null, 2)}
          </pre>
        </div>
      </div>

      {/* Claims Breakdown Table */}
      <div className="flex flex-col gap-2">
        <div className="text-[11px] font-bold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
          <Info className="w-3.5 h-3.5 text-blue-400" /> Kluczowe roszczenia (Claims Breakdown)
        </div>
        <div className="border border-slate-800 rounded-lg overflow-hidden">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-900 text-slate-400 font-mono border-b border-slate-800">
              <tr>
                <th className="p-2">Roszczenie (Claim)</th>
                <th className="p-2">Wartość</th>
                <th className="p-2 hidden sm:table-cell">Opis</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-mono text-[11px]">
              {jwt.payload.iss !== undefined && (
                <tr className="hover:bg-slate-900/40">
                  <td className="p-2 text-blue-400 font-semibold">iss</td>
                  <td className="p-2 text-slate-200">{String(jwt.payload.iss)}</td>
                  <td className="p-2 text-slate-400 hidden sm:table-cell">Issuer (Wydawca tokenu OIDC)</td>
                </tr>
              )}
              {jwt.payload.sub !== undefined && (
                <tr className="hover:bg-slate-900/40">
                  <td className="p-2 text-blue-400 font-semibold">sub</td>
                  <td className="p-2 text-slate-200">{String(jwt.payload.sub)}</td>
                  <td className="p-2 text-slate-400 hidden sm:table-cell">Subject (ID użytkownika / podmiotu)</td>
                </tr>
              )}
              {jwt.payload.client_id !== undefined && (
                <tr className="hover:bg-slate-900/40">
                  <td className="p-2 text-blue-400 font-semibold">client_id</td>
                  <td className="p-2 text-slate-200">{String(jwt.payload.client_id)}</td>
                  <td className="p-2 text-slate-400 hidden sm:table-cell">Identyfikator autoryzowanego klienta</td>
                </tr>
              )}
              {jwt.payload.scope !== undefined && (
                <tr className="hover:bg-slate-900/40">
                  <td className="p-2 text-blue-400 font-semibold">scope</td>
                  <td className="p-2 text-emerald-300 font-semibold">{String(jwt.payload.scope)}</td>
                  <td className="p-2 text-slate-400 hidden sm:table-cell">Przyznane zakresy uprawnień</td>
                </tr>
              )}
              {jwt.payload.aud !== undefined && (
                <tr className="hover:bg-slate-900/40">
                  <td className="p-2 text-blue-400 font-semibold">aud</td>
                  <td className="p-2 text-slate-200">{JSON.stringify(jwt.payload.aud)}</td>
                  <td className="p-2 text-slate-400 hidden sm:table-cell">Audience (Odbiorca / API Docelowe)</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
