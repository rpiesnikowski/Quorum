export interface IdentityUser {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  roles: string[];
  claims: Record<string, string>;
  isLockedOut: boolean;
  emailConfirmed: boolean;
  avatarColor: string;
}

export interface AuthZenSubject {
  type: string;
  id: string;
  properties?: Record<string, any>;
}

export interface AuthZenAction {
  name: string;
}

export interface AuthZenResource {
  type: string;
  id: string;
  properties?: Record<string, any>;
}

export interface AuthZenContext {
  clientIp?: string;
  environment?: string;
  requestTime?: string;
  [key: string]: any;
}

export interface AuthZenEvaluationRequest {
  subject: AuthZenSubject;
  action: AuthZenAction;
  resource: AuthZenResource;
  context?: AuthZenContext;
}

export interface AuthZenPolicy {
  id: number;
  name: string;
  description: string;
  priority: number;
  effect: 'Permit' | 'Deny';
  resourceType: string;
  resourcePattern: string;
  action: string;
  subjectRoles: string[];
  subjectRequiredClaims?: Record<string, string>;
  requireAuthenticated: boolean;
  requireActiveUser: boolean;
  conditionExpression?: string;
  isEnabled: boolean;
}

export interface PdpEvaluationTraceStep {
  policyId: number;
  policyName: string;
  priority: number;
  effect: 'Permit' | 'Deny';
  resourceMatched: boolean;
  actionMatched: boolean;
  subjectMatched: boolean;
  result: 'Permit' | 'Deny' | 'Skipped' | 'Mismatch';
  reason: string;
}

export interface PdpEvaluationResult {
  decision: boolean;
  matchedPolicy: AuthZenPolicy | null;
  executionTimeMs: number;
  reason: string;
  pipData: IdentityUser | null;
  trace: PdpEvaluationTraceStep[];
  requestPayload: AuthZenEvaluationRequest;
  responsePayload: {
    decision: boolean;
    context: Record<string, any>;
  };
  timestamp: string;
}
