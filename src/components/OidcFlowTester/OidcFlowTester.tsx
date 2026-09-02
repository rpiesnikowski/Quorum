import React, { useState, useEffect } from 'react';
import { 
  KeyRound, 
  Server, 
  Compass, 
  Bookmark, 
  HelpCircle, 
  Layers, 
  ShieldCheck, 
  Radio, 
  Terminal, 
  CheckCircle2, 
  Lock, 
  ExternalLink,
  ChevronDown,
  ChevronUp
} from 'lucide-react';
import { 
  OidcFlowType, 
  AuthCodeConfig, 
  ClientCredentialsConfig, 
  DiscoveryConfig,
  PRESET_PROFILES,
  PresetProfile
} from '../../types/oidc';
import { AuthCodeFlowView } from './AuthCodeFlowView';
import { ClientCredentialsFlowView } from './ClientCredentialsFlowView';
import { DiscoveryFlowView } from './DiscoveryFlowView';
import { generatePkcePair, generateRandomString } from '../../utils/oidcCrypto';

export const OidcFlowTester: React.FC = () => {
  const [activeFlow, setActiveFlow] = useState<OidcFlowType>('authorization_code');
  const [issuerUrl, setIssuerUrl] = useState<string>('https://localhost:5001');
  const [selectedPresetId, setSelectedPresetId] = useState<string>('spa-pkce');
  const [showFlowGuide, setShowFlowGuide] = useState(false);

  // Stan konfiguracji Authorization Code
  const [authCodeConfig, setAuthCodeConfig] = useState<AuthCodeConfig>({
    issuerUrl: 'https://localhost:5001',
    authorizeEndpoint: '/connect/authorize',
    tokenEndpoint: '/connect/token',
    clientId: 'frontend-spa-portal',
    clientSecret: '',
    redirectUri: 'http://localhost:3000/callback',
    scopes: ['openid', 'profile', 'email', 'quorum.api', 'quorum.gateway'],
    responseType: 'code',
    responseMode: 'query',
    state: 'state_random_' + Math.random().toString(36).substring(2, 10),
    nonce: 'nonce_random_' + Math.random().toString(36).substring(2, 10),
    usePkce: true,
    codeVerifier: 'mock_code_verifier_initial_value_for_pkce_testing_987654321',
    codeChallenge: 'E9Melhoa2OwvFrGMTJguCH5ZiXVupWhMzuzfinAm_FY',
    codeChallengeMethod: 'S256',
    authCode: 'mock_auth_code_' + Math.random().toString(36).substring(2, 12),
    authMethod: 'none'
  });

  // Stan konfiguracji Client Credentials
  const [clientCredentialsConfig, setClientCredentialsConfig] = useState<ClientCredentialsConfig>({
    issuerUrl: 'https://localhost:5001',
    tokenEndpoint: '/connect/token',
    clientId: 'backend-worker-service',
    clientSecret: 'Pass123$',
    scopes: ['quorum.api', 'telemetry.read', 'quorum.admin'],
    authMethod: 'client_secret_post'
  });

  // Stan konfiguracji Discovery
  const [discoveryConfig, setDiscoveryConfig] = useState<DiscoveryConfig>({
    issuerUrl: 'https://localhost:5001',
    discoveryPath: '/.well-known/openid-configuration'
  });

  // Inicjalizacja prawdziwego PKCE na starcie
  useEffect(() => {
    generatePkcePair().then((pair) => {
      const state = generateRandomString(24);
      const nonce = generateRandomString(24);
      const authCode = 'mock_auth_code_' + generateRandomString(30);

      setAuthCodeConfig((prev) => ({
        ...prev,
        codeVerifier: pair.verifier,
        codeChallenge: pair.challenge,
        state,
        nonce,
        authCode
      }));
    });
  }, []);

  // Synchronizacja adresu Issuer URL we wszystkich konfiguracjach
  const handleIssuerChange = (newUrl: string) => {
    setIssuerUrl(newUrl);
    setAuthCodeConfig((prev) => ({ ...prev, issuerUrl: newUrl }));
    setClientCredentialsConfig((prev) => ({ ...prev, issuerUrl: newUrl }));
    setDiscoveryConfig((prev) => ({ ...prev, issuerUrl: newUrl }));
  };

  // Aplikowanie profilu szablonowego
  const applyPreset = (preset: PresetProfile) => {
    setSelectedPresetId(preset.id);
    setActiveFlow(preset.flow);

    if (preset.authCodeConfig) {
      setAuthCodeConfig((prev) => ({
        ...prev,
        ...preset.authCodeConfig,
        issuerUrl
      }));
    }

    if (preset.clientCredentialsConfig) {
      setClientCredentialsConfig((prev) => ({
        ...prev,
        ...preset.clientCredentialsConfig,
        issuerUrl
      }));
    }
  };

  return (
    <div className="flex flex-col gap-6">
      {/* Top Header & Overview Bar */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 sm:p-6 shadow-sm">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 text-blue-400 text-xs font-semibold uppercase tracking-wider mb-1">
              <Radio className="w-4 h-4 text-emerald-400 animate-pulse" />
              Tester Przepływów OIDC & OAuth 2.0
            </div>
            <h2 className="text-xl font-bold text-white">
              Weryfikator Endpoints i Generator Żądań OpenID Connect
            </h2>
            <p className="text-sm text-slate-400 mt-1 max-w-3xl">
              Narzędzie dla deweloperów umożliwiające generowanie próbnych zapytań autoryzacyjnych, wymianę kodów <code className="text-blue-300">authorization_code</code> z PKCE oraz poświadczeń <code className="text-purple-300">client_credentials</code> w celu testowania lokalnych endpointów serwera Quorum.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2 self-start lg:self-auto">
            <button
              onClick={() => setShowFlowGuide(!showFlowGuide)}
              className="flex items-center gap-1.5 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs font-medium transition-colors cursor-pointer border border-slate-700"
            >
              <HelpCircle className="w-4 h-4 text-blue-400" />
              <span>Schemat Przepływów RFC</span>
              {showFlowGuide ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
            </button>
          </div>
        </div>

        {/* Collapsible Flow Guide */}
        {showFlowGuide && (
          <div className="mt-5 pt-5 border-t border-slate-800 grid grid-cols-1 md:grid-cols-2 gap-4 text-xs text-slate-300">
            <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col gap-2">
              <div className="font-bold text-sm text-emerald-400 flex items-center gap-1.5">
                <ShieldCheck className="w-4 h-4" />
                Authorization Code Flow + PKCE (RFC 7636)
              </div>
              <p className="text-slate-400 leading-relaxed">
                Stosowany w aplikacjach jednostronicowych (SPA) oraz mobilnych. Klient generuje losowy sekret <code className="text-slate-200">code_verifier</code> oraz jego skrót SHA-256 <code className="text-slate-200">code_challenge</code>.
              </p>
              <ol className="list-decimal pl-4 space-y-1 text-slate-300">
                <li>Przekierowanie do <code className="text-blue-400">/connect/authorize</code> z <code className="text-emerald-300">code_challenge</code>.</li>
                <li>Użytkownik loguje się i akceptuje uprawnienia.</li>
                <li>Serwer odsyła jednorazowy <code className="text-amber-300">code</code> na zarejestrowany <code className="text-blue-400">redirect_uri</code>.</li>
                <li>Aplikacja wysyła <code className="text-emerald-300">code_verifier</code> do <code className="text-blue-400">/connect/token</code> w celu odbioru tokenów.</li>
              </ol>
            </div>

            <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 flex flex-col gap-2">
              <div className="font-bold text-sm text-purple-400 flex items-center gap-1.5">
                <Server className="w-4 h-4" />
                Client Credentials Flow (RFC 6749)
              </div>
              <p className="text-slate-400 leading-relaxed">
                Stosowany do bezpiecznej komunikacji usługa-usługa (Machine-to-Machine) w tle, np. mikrousługi backendowe, daemony czy API Gateway Proxy.
              </p>
              <ol className="list-decimal pl-4 space-y-1 text-slate-300">
                <li>Serwis wysyła bezpośrednie żądanie POST do <code className="text-blue-400">/connect/token</code>.</li>
                <li>Przekazuje <code className="text-purple-300">client_id</code> oraz <code className="text-purple-300">client_secret</code> (w nagłówku Basic lub w ciele żądania).</li>
                <li>Serwer natychmiast weryfikuje poświadczenia i zwraca token z wybranymi scope’ami.</li>
              </ol>
            </div>
          </div>
        )}

        {/* Server Authority & Local URL Configuration Bar */}
        <div className="mt-5 pt-4 border-t border-slate-800/80 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-2 flex-1 max-w-xl">
            <span className="text-xs font-semibold text-slate-400 shrink-0">
              Lokalny Serwer (Issuer):
            </span>
            <div className="flex-1 flex rounded-lg overflow-hidden border border-slate-700 bg-slate-950">
              <input
                type="text"
                value={issuerUrl}
                onChange={(e) => handleIssuerChange(e.target.value)}
                className="w-full px-3 py-1.5 text-xs font-mono text-white outline-none bg-transparent"
                placeholder="https://localhost:5001"
              />
            </div>
            <div className="flex gap-1">
              <button
                onClick={() => handleIssuerChange('https://localhost:5001')}
                className={`px-2 py-1 text-[11px] font-mono rounded cursor-pointer transition-colors ${
                  issuerUrl === 'https://localhost:5001'
                    ? 'bg-blue-600 text-white font-semibold'
                    : 'bg-slate-800 text-slate-400 hover:text-white'
                }`}
                title="Domyślny port HTTPS dla Quorum.Backend"
              >
                :5001 (HTTPS)
              </button>
              <button
                onClick={() => handleIssuerChange('http://localhost:5000')}
                className={`px-2 py-1 text-[11px] font-mono rounded cursor-pointer transition-colors ${
                  issuerUrl === 'http://localhost:5000'
                    ? 'bg-blue-600 text-white font-semibold'
                    : 'bg-slate-800 text-slate-400 hover:text-white'
                }`}
                title="Domyślny port HTTP dla Quorum.Backend"
              >
                :5000 (HTTP)
              </button>
            </div>
          </div>

          {/* Quick Presets Dropdown / Buttons */}
          <div className="flex items-center gap-2 overflow-x-auto">
            <span className="text-xs text-slate-500 shrink-0 flex items-center gap-1">
              <Bookmark className="w-3.5 h-3.5" /> Szablony:
            </span>
            {PRESET_PROFILES.map((preset) => {
              const isSelected = selectedPresetId === preset.id;
              return (
                <button
                  key={preset.id}
                  onClick={() => applyPreset(preset)}
                  className={`px-2.5 py-1 text-xs rounded-lg whitespace-nowrap cursor-pointer transition-all ${
                    isSelected
                      ? `${preset.badgeColor} border font-semibold shadow-sm`
                      : 'bg-slate-800/80 text-slate-400 hover:text-slate-200 border border-transparent'
                  }`}
                  title={preset.description}
                >
                  {preset.name}
                </button>
              );
            })}
          </div>
        </div>

        {/* Primary Flow Navigation Tabs */}
        <div className="flex flex-wrap gap-2 border-t border-slate-800/80 mt-5 pt-4">
          <button
            onClick={() => setActiveFlow('authorization_code')}
            className={`flex items-center gap-2 px-4 py-2.5 text-xs font-semibold rounded-xl transition-all cursor-pointer ${
              activeFlow === 'authorization_code'
                ? 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/40 shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <KeyRound className="w-4 h-4 text-emerald-400" />
            <span>Authorization Code Flow (+ PKCE)</span>
            <span className="px-1.5 py-0.2 rounded text-[10px] bg-emerald-500/10 text-emerald-300">
              Interactive
            </span>
          </button>

          <button
            onClick={() => setActiveFlow('client_credentials')}
            className={`flex items-center gap-2 px-4 py-2.5 text-xs font-semibold rounded-xl transition-all cursor-pointer ${
              activeFlow === 'client_credentials'
                ? 'bg-purple-600/20 text-purple-400 border border-purple-500/40 shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Server className="w-4 h-4 text-purple-400" />
            <span>Client Credentials Flow</span>
            <span className="px-1.5 py-0.2 rounded text-[10px] bg-purple-500/10 text-purple-300">
              M2M
            </span>
          </button>

          <button
            onClick={() => setActiveFlow('discovery')}
            className={`flex items-center gap-2 px-4 py-2.5 text-xs font-semibold rounded-xl transition-all cursor-pointer ${
              activeFlow === 'discovery'
                ? 'bg-teal-600/20 text-teal-400 border border-teal-500/40 shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
            }`}
          >
            <Compass className="w-4 h-4 text-teal-400" />
            <span>Discovery (.well-known)</span>
            <span className="px-1.5 py-0.2 rounded text-[10px] bg-teal-500/10 text-teal-300">
              RFC 8414
            </span>
          </button>
        </div>
      </div>

      {/* Main Flow Content Component */}
      {activeFlow === 'authorization_code' && (
        <AuthCodeFlowView
          config={authCodeConfig}
          onChangeConfig={(up) => setAuthCodeConfig((prev) => ({ ...prev, ...up }))}
        />
      )}

      {activeFlow === 'client_credentials' && (
        <ClientCredentialsFlowView
          config={clientCredentialsConfig}
          onChangeConfig={(up) => setClientCredentialsConfig((prev) => ({ ...prev, ...up }))}
        />
      )}

      {activeFlow === 'discovery' && (
        <DiscoveryFlowView
          config={discoveryConfig}
          onChangeConfig={(up) => setDiscoveryConfig((prev) => ({ ...prev, ...up }))}
        />
      )}
    </div>
  );
};
