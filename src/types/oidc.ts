export type OidcFlowType = 'authorization_code' | 'client_credentials' | 'discovery';

export type ClientAuthMethod = 'client_secret_post' | 'client_secret_basic' | 'none';

export interface AuthCodeConfig {
  issuerUrl: string;
  authorizeEndpoint: string;
  tokenEndpoint: string;
  clientId: string;
  clientSecret: string;
  redirectUri: string;
  scopes: string[];
  responseType: string;
  responseMode: string;
  state: string;
  nonce: string;
  usePkce: boolean;
  codeVerifier: string;
  codeChallenge: string;
  codeChallengeMethod: 'S256' | 'plain';
  authCode: string;
  authMethod: ClientAuthMethod;
}

export interface ClientCredentialsConfig {
  issuerUrl: string;
  tokenEndpoint: string;
  clientId: string;
  clientSecret: string;
  scopes: string[];
  authMethod: 'client_secret_post' | 'client_secret_basic';
}

export interface DiscoveryConfig {
  issuerUrl: string;
  discoveryPath: string;
}

export interface DecodedJwt {
  header: Record<string, unknown>;
  payload: Record<string, unknown>;
  signature: string;
  raw: string;
  isValidStructure: boolean;
  expiresAt?: Date;
  isExpired?: boolean;
}

export interface OidcResponse {
  status: number;
  statusText: string;
  durationMs: number;
  headers: Record<string, string>;
  body: Record<string, unknown> | string;
  isError: boolean;
  isSimulated?: boolean;
  timestamp: string;
  rawJson?: string;
  decodedAccessToken?: DecodedJwt | null;
  decodedIdToken?: DecodedJwt | null;
}

export interface PresetProfile {
  id: string;
  name: string;
  flow: OidcFlowType;
  description: string;
  tag: string;
  badgeColor: string;
  authCodeConfig?: Partial<AuthCodeConfig>;
  clientCredentialsConfig?: Partial<ClientCredentialsConfig>;
}

export const PRESET_PROFILES: PresetProfile[] = [
  {
    id: 'spa-pkce',
    name: 'Frontend SPA (React/Vue/Blazor)',
    flow: 'authorization_code',
    description: 'Authorization Code Flow z PKCE (S256), publiczny klient bez sekretu w przeglądarce.',
    tag: 'Zalecany dla SPA',
    badgeColor: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20',
    authCodeConfig: {
      clientId: 'frontend-spa-portal',
      clientSecret: '',
      authMethod: 'none',
      usePkce: true,
      codeChallengeMethod: 'S256',
      redirectUri: 'http://localhost:3000/callback',
      scopes: ['openid', 'profile', 'email', 'quorum.api', 'quorum.gateway'],
      responseType: 'code',
      responseMode: 'query'
    }
  },
  {
    id: 'confidential-web',
    name: 'Aplikacja Serwerowa (MVC / Razor / BFF)',
    flow: 'authorization_code',
    description: 'Poufny klient serwerowy z Client Secret i PKCE, obsługujący refresh tokeny.',
    tag: 'Web App BFF',
    badgeColor: 'bg-blue-500/10 text-blue-400 border-blue-500/20',
    authCodeConfig: {
      clientId: 'quorum_web_client',
      clientSecret: 'secret_web_client_987$',
      authMethod: 'client_secret_post',
      usePkce: true,
      codeChallengeMethod: 'S256',
      redirectUri: 'https://localhost:5001/signin-oidc',
      scopes: ['openid', 'profile', 'email', 'quorum.api', 'offline_access'],
      responseType: 'code',
      responseMode: 'query'
    }
  },
  {
    id: 'm2m-worker',
    name: 'Serwis Backend M2M / Worker Daemon',
    flow: 'client_credentials',
    description: 'Komunikacja usługa-usługa bez udziału użytkownika (Machine to Machine).',
    tag: 'M2M Daemon',
    badgeColor: 'bg-purple-500/10 text-purple-400 border-purple-500/20',
    clientCredentialsConfig: {
      clientId: 'backend-worker-service',
      clientSecret: 'Pass123$',
      authMethod: 'client_secret_post',
      scopes: ['quorum.api', 'telemetry.read', 'quorum.admin']
    }
  },
  {
    id: 'api-gateway-proxy',
    name: 'Bramka API Gateway Proxy',
    flow: 'client_credentials',
    description: 'Klient bramki Quorum.Backend.Gateway autoryzujący zapytania w architekturze mikroserwisów.',
    tag: 'Gateway Proxy',
    badgeColor: 'bg-cyan-500/10 text-cyan-400 border-cyan-500/20',
    clientCredentialsConfig: {
      clientId: 'quorum-gateway-proxy',
      clientSecret: 'GatewaySecret2026!#',
      authMethod: 'client_secret_basic',
      scopes: ['quorum.gateway', 'proxy.all', 'telemetry.write']
    }
  }
];

export const AVAILABLE_SCOPES = [
  { id: 'openid', label: 'openid', desc: 'Identyfikator OIDC (wymagany dla ID Token)' },
  { id: 'profile', label: 'profile', desc: 'Imię, nazwisko, preferencje konta' },
  { id: 'email', label: 'email', desc: 'Adres e-mail i potwierdzenie weryfikacji' },
  { id: 'quorum.api', label: 'quorum.api', desc: 'Dostęp do głównego REST API Quorum' },
  { id: 'quorum.gateway', label: 'quorum.gateway', desc: 'Dostęp przez warstwę Proxy API Gateway' },
  { id: 'telemetry.read', label: 'telemetry.read', desc: 'Odczyt metryk OpenTelemetry i śladów' },
  { id: 'quorum.admin', label: 'quorum.admin', desc: 'Uprawnienia administracyjne do zasobów' },
  { id: 'offline_access', label: 'offline_access', desc: 'Zezwolenie na wydawanie Refresh Tokenów' },
];
