import { IdentityUser, AuthZenPolicy, AuthZenEvaluationRequest, PdpEvaluationResult, PdpEvaluationTraceStep } from '../types/authzen';

export const INITIAL_IDENTITY_USERS: IdentityUser[] = [
  {
    id: 'usr_01_admin',
    userName: 'admin',
    email: 'admin@quorum.local',
    fullName: 'Główny Administrator Systemu',
    roles: ['Admin', 'SuperAdmin'],
    claims: {
      department: 'Security',
      clearance: 'Level-5',
      tenant_id: 'tenant-primary'
    },
    isLockedOut: false,
    emailConfirmed: true,
    avatarColor: 'from-blue-600 to-indigo-600'
  },
  {
    id: 'usr_02_manager',
    userName: 'jkowalski',
    email: 'jan.kowalski@quorum.pl',
    fullName: 'Jan Kowalski (Dyrektor Finansowy)',
    roles: ['Manager', 'BillingAdmin'],
    claims: {
      department: 'Finance',
      cost_center: 'CC-401',
      spending_limit: '100000'
    },
    isLockedOut: false,
    emailConfirmed: true,
    avatarColor: 'from-emerald-600 to-teal-600'
  },
  {
    id: 'usr_03_dev',
    userName: 'anowak',
    email: 'anna.nowak@tech.io',
    fullName: 'Anna Nowak (Senior Developer)',
    roles: ['Developer', 'User'],
    claims: {
      department: 'Engineering',
      project: 'Quorum.Backend',
      environment_access: 'staging'
    },
    isLockedOut: false,
    emailConfirmed: true,
    avatarColor: 'from-purple-600 to-pink-600'
  },
  {
    id: 'usr_04_operator',
    userName: 'krzysztof_ops',
    email: 'operator.krzysztof@ops.net',
    fullName: 'Krzysztof Wiśniewski (DevOps & Telemetria)',
    roles: ['Operator', 'User'],
    claims: {
      department: 'Operations',
      shift: 'Day',
      cluster: 'eu-west-1'
    },
    isLockedOut: false,
    emailConfirmed: true,
    avatarColor: 'from-amber-600 to-orange-600'
  },
  {
    id: 'usr_05_auditor',
    userName: 'auditor_ext',
    email: 'guest.auditor@compliance.org',
    fullName: 'Marta Zielińska (Audytor Zewnętrzny)',
    roles: ['Auditor'],
    claims: {
      department: 'Compliance',
      read_only_scope: 'full',
      audit_ref: 'AUD-2026-Q3'
    },
    isLockedOut: false,
    emailConfirmed: true,
    avatarColor: 'from-cyan-600 to-blue-600'
  },
  {
    id: 'usr_06_locked',
    userName: 'bad_actor',
    email: 'locked.user@suspicious.net',
    fullName: 'Konto Zablokowane (Naruszenie Polityki)',
    roles: ['User'],
    claims: {
      incident_id: 'SEC-9912',
      risk_score: '98'
    },
    isLockedOut: true,
    emailConfirmed: false,
    avatarColor: 'from-rose-600 to-red-600'
  }
];

export const INITIAL_AUTHZEN_POLICIES: AuthZenPolicy[] = [
  {
    id: 1,
    name: 'DenyLockedOutUsers',
    description: 'Blokuje wszelki dostęp dla użytkowników oznaczonych jako LockedOut w ASP.NET Identity.',
    priority: 1000,
    effect: 'Deny',
    resourceType: '*',
    resourcePattern: '*',
    action: '*',
    subjectRoles: [],
    requireAuthenticated: true,
    requireActiveUser: true, // Fail if locked
    isEnabled: true
  },
  {
    id: 2,
    name: 'RequireAdminForBillingApi',
    description: 'Zezwala wyłącznie użytkownikom z rolą Admin lub BillingAdmin na dostęp do endpointów fakturowania.',
    priority: 900,
    effect: 'Permit',
    resourceType: 'route',
    resourcePattern: '/api/v1/billing/*',
    action: '*',
    subjectRoles: ['Admin', 'BillingAdmin'],
    requireAuthenticated: true,
    requireActiveUser: true,
    isEnabled: true
  },
  {
    id: 3,
    name: 'DenyDestructiveActionsForAuditors',
    description: 'Bezwzględny zakaz modyfikacji danych (write, POST, PUT, DELETE) dla audytorów.',
    priority: 850,
    effect: 'Deny',
    resourceType: '*',
    resourcePattern: '*',
    action: 'write, POST, PUT, DELETE',
    subjectRoles: ['Auditor'],
    requireAuthenticated: true,
    requireActiveUser: true,
    isEnabled: true
  },
  {
    id: 4,
    name: 'RequireOperatorForDeviceTelemetry',
    description: 'Zarządzanie telemetrią i urządzeniami wymaga roli Operator lub Admin.',
    priority: 750,
    effect: 'Permit',
    resourceType: 'route',
    resourcePattern: '/api/v1/devices/*',
    action: 'execute, write, POST, PUT',
    subjectRoles: ['Operator', 'Admin'],
    requireAuthenticated: true,
    requireActiveUser: true,
    isEnabled: true
  },
  {
    id: 5,
    name: 'PermitOrdersReadForAuthenticatedUsers',
    description: 'Zezwala wszystkim aktywnym użytkownikom biznesowym na odczyt zamówień.',
    priority: 600,
    effect: 'Permit',
    resourceType: 'route',
    resourcePattern: '/api/v1/orders*',
    action: 'read, GET',
    subjectRoles: ['User', 'Manager', 'Admin', 'Developer', 'Auditor', 'Operator'],
    requireAuthenticated: true,
    requireActiveUser: true,
    isEnabled: true
  },
  {
    id: 6,
    name: 'RestrictOrdersWriteToManagers',
    description: 'Tworzenie i anulowanie zamówień zarezerwowane dla Managerów i Administratorów.',
    priority: 650,
    effect: 'Permit',
    resourceType: 'route',
    resourcePattern: '/api/v1/orders*',
    action: 'write, POST, PUT, DELETE',
    subjectRoles: ['Manager', 'Admin'],
    requireAuthenticated: true,
    requireActiveUser: true,
    isEnabled: true
  },
  {
    id: 7,
    name: 'PermitPublicEndpoints',
    description: 'Dostęp publiczny bez uwierzytelnienia do statusu systemu i dokumentacji Swagger.',
    priority: 100,
    effect: 'Permit',
    resourceType: 'route',
    resourcePattern: '/public/*, /health, /api/docs/*',
    action: 'read, GET',
    subjectRoles: [],
    requireAuthenticated: false,
    requireActiveUser: false,
    isEnabled: true
  }
];

export interface PresetScenario {
  id: string;
  title: string;
  description: string;
  expectedDecision: boolean;
  userId: string;
  action: string;
  resourceId: string;
}

export const PRESET_SCENARIOS: PresetScenario[] = [
  {
    id: 'billing_admin',
    title: 'Administrator otwiera panel Billing (/api/v1/billing/invoices)',
    description: 'Admin posiada rolę "Admin", co spełnia politykę RequireAdminForBillingApi.',
    expectedDecision: true,
    userId: 'usr_01_admin',
    action: 'GET',
    resourceId: '/api/v1/billing/invoices/2026'
  },
  {
    id: 'billing_dev_denied',
    title: 'Programista próbuje pobrać faktury (/api/v1/billing/export)',
    description: 'Developer nie posiada roli Admin ani BillingAdmin, PDP zwróci odmowę (Deny).',
    expectedDecision: false,
    userId: 'usr_03_dev',
    action: 'GET',
    resourceId: '/api/v1/billing/export'
  },
  {
    id: 'manager_creates_order',
    title: 'Manager tworzy nowe zamówienie (POST /api/v1/orders)',
    description: 'Manager posiada rolę "Manager", co spełnia politykę RestrictOrdersWriteToManagers.',
    expectedDecision: true,
    userId: 'usr_02_manager',
    action: 'POST',
    resourceId: '/api/v1/orders'
  },
  {
    id: 'auditor_deletes_order',
    title: 'Audytor próbuje usunąć zamówienie (DELETE /api/v1/orders/501)',
    description: 'Polityka DenyDestructiveActionsForAuditors blokuje usuwanie danych przez Audytora.',
    expectedDecision: false,
    userId: 'usr_05_auditor',
    action: 'DELETE',
    resourceId: '/api/v1/orders/501'
  },
  {
    id: 'locked_user_denied',
    title: 'Zablokowany użytkownik Identity próbuje odczytać zamówienia',
    description: 'Konto o statusie IsLockedOut=true narusza politykę DenyLockedOutUsers z priorytetem 1000.',
    expectedDecision: false,
    userId: 'usr_06_locked',
    action: 'GET',
    resourceId: '/api/v1/orders'
  },
  {
    id: 'anonymous_health_check',
    title: 'Anonimowy klient wywołuje health check (/public/health)',
    description: 'Trasa publiczna nie wymaga uwierzytelnienia, polityka PermitPublicEndpoints zezwala.',
    expectedDecision: true,
    userId: '',
    action: 'GET',
    resourceId: '/public/health'
  }
];

// Funkcja dopasowywania wzorców ścieżek zasobów (wildcards '*' i regex)
function matchResourcePattern(pattern: string, target: string): boolean {
  if (pattern === '*' || pattern === target) return true;
  const subPatterns = pattern.split(',').map(s => s.trim());
  for (const p of subPatterns) {
    if (p === target) return true;
    if (p.endsWith('/*')) {
      const prefix = p.slice(0, -2);
      if (target.startsWith(prefix)) return true;
    }
    if (p.endsWith('*')) {
      const prefix = p.slice(0, -1);
      if (target.startsWith(prefix)) return true;
    }
  }
  return false;
}

// Funkcja dopasowywania akcji
function matchAction(policyAction: string, requestedAction: string): boolean {
  if (policyAction === '*' || policyAction.trim() === '') return true;
  const actions = policyAction.split(',').map(a => a.trim().toLowerCase());
  return actions.includes(requestedAction.trim().toLowerCase());
}

/**
 * Ewaluator silnika AuthZEN PDP (Policy Decision Point)
 */
export function evaluateAuthZenPolicy(
  request: AuthZenEvaluationRequest,
  policies: AuthZenPolicy[],
  identityUsers: IdentityUser[]
): PdpEvaluationResult {
  const startTime = performance.now();

  // PIP (Policy Information Point) - Pobieramy tożsamość z Identity
  let user: IdentityUser | null = null;
  const isAnonymous = !request.subject.id || request.subject.id === 'anonymous';

  if (!isAnonymous) {
    user = identityUsers.find(u => u.id === request.subject.id || u.userName.toLowerCase() === request.subject.id.toLowerCase() || u.email.toLowerCase() === request.subject.id.toLowerCase()) || null;
  }

  // Sortujemy aktywne polityki malejąco według priorytetu
  const activePolicies = policies
    .filter(p => p.isEnabled)
    .sort((a, b) => b.priority - a.priority);

  const trace: PdpEvaluationTraceStep[] = [];
  let decision: boolean = false;
  let matchedPolicy: AuthZenPolicy | null = null;
  let reason: string = 'Domyślna odmowa silnika decyzyjnego PDP (Brak pasującej polityki zezwalającej).';

  for (const policy of activePolicies) {
    const resourceMatched = matchResourcePattern(policy.resourcePattern, request.resource.id);
    const actionMatched = matchAction(policy.action, request.action.name);

    // Weryfikacja wymogu uwierzytelnienia
    let subjectAuthOk = true;
    if (policy.requireAuthenticated && isAnonymous) {
      subjectAuthOk = false;
    }

    // Weryfikacja blokady konta (Lockout w Identity)
    let activeUserOk = true;
    if (policy.requireActiveUser && user && user.isLockedOut) {
      activeUserOk = false;
    }

    // Weryfikacja ról (RBAC)
    let rolesOk = true;
    if (policy.subjectRoles.length > 0) {
      if (!user) {
        rolesOk = false;
      } else {
        const hasRole = policy.subjectRoles.some(r => 
          user!.roles.map(x => x.toLowerCase()).includes(r.toLowerCase())
        );
        if (!hasRole) {
          rolesOk = false;
        }
      }
    }

    const subjectMatched = subjectAuthOk && activeUserOk && rolesOk;

    // Ewaluacja kroku
    if (resourceMatched && actionMatched) {
      if (subjectMatched) {
        if (policy.effect === 'Deny') {
          decision = false;
          matchedPolicy = policy;
          reason = `Polityka blokująca (Deny): '${policy.name}' dopasowała żądanie i odmówiła dostępu.`;
          trace.push({
            policyId: policy.id,
            policyName: policy.name,
            priority: policy.priority,
            effect: policy.effect,
            resourceMatched,
            actionMatched,
            subjectMatched: true,
            result: 'Deny',
            reason: 'Zastosowano regułę odmowy (Effect: Deny).'
          });
          break; // Wzorzec Deny przerywa dalsze zezwolenia o niższym priorytecie
        } else if (policy.effect === 'Permit') {
          decision = true;
          matchedPolicy = policy;
          reason = `Polityka '${policy.name}' (Priorytet: ${policy.priority}) wydała decyzję zezwalającą (Permit).`;
          trace.push({
            policyId: policy.id,
            policyName: policy.name,
            priority: policy.priority,
            effect: policy.effect,
            resourceMatched,
            actionMatched,
            subjectMatched: true,
            result: 'Permit',
            reason: 'Wszystkie kryteria zasobu, akcji i podmiotu spełnione.'
          });
          break; // Zezwolono na podstawie najwyższego priorytetu
        }
      } else {
        let mismatchDetails: string[] = [];
        if (!subjectAuthOk) mismatchDetails.push('Wymagane uwierzytelnienie');
        if (!activeUserOk) mismatchDetails.push('Użytkownik zablokowany (IsLockedOut=true)');
        if (!rolesOk) mismatchDetails.push(`Brak wymaganej roli: [${policy.subjectRoles.join(', ')}]`);

        trace.push({
          policyId: policy.id,
          policyName: policy.name,
          priority: policy.priority,
          effect: policy.effect,
          resourceMatched,
          actionMatched,
          subjectMatched: false,
          result: 'Mismatch',
          reason: `Zasób i akcja pasują, ale podmiot nie spełnia warunków: ${mismatchDetails.join('; ')}`
        });
      }
    } else {
      trace.push({
        policyId: policy.id,
        policyName: policy.name,
        priority: policy.priority,
        effect: policy.effect,
        resourceMatched,
        actionMatched,
        subjectMatched,
        result: 'Skipped',
        reason: !resourceMatched ? 'Niezgodny wzorzec zasobu' : 'Niezgodna akcja'
      });
    }
  }

  const endTime = performance.now();
  const executionTimeMs = Number((endTime - startTime).toFixed(2));

  const responsePayload = {
    decision,
    context: {
      evaluating_pdp: 'Quorum-AuthZen-PDP-Engine/v1.0-net10',
      timestamp: new Date().toISOString(),
      matched_policy: matchedPolicy ? {
        id: matchedPolicy.id,
        name: matchedPolicy.name,
        priority: matchedPolicy.priority,
        effect: matchedPolicy.effect
      } : null,
      pip_identity: user ? {
        id: user.id,
        user_name: user.userName,
        email: user.email,
        roles: user.roles,
        claims: user.claims,
        is_locked_out: user.isLockedOut
      } : {
        type: 'anonymous',
        authenticated: false
      },
      pdp_reason: reason
    }
  };

  return {
    decision,
    matchedPolicy,
    executionTimeMs,
    reason,
    pipData: user,
    trace,
    requestPayload: request,
    responsePayload,
    timestamp: new Date().toLocaleTimeString()
  };
}
