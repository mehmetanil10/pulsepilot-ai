export type AuthUser = {
  userId: string;
  email: string;
  displayName: string;
  workspaceId: string;
  workspaceName?: string;
  role: string;
};

export type AuthSuccess = {
  user: AuthUser;
  expiresAt: string;
};

export type BackendAuthenticationResponse = AuthUser & {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  workspaceName: string;
};

export type BackendCurrentUserResponse = Omit<AuthUser, "workspaceName">;
