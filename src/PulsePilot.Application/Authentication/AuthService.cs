using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Application.Authentication;

internal sealed class AuthService(
    IUserRepository userRepository,
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository workspaceMemberRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthenticationResponse> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedEmail = NormalizeEmail(command.Email);

        if (await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = User.Create(
            command.Email,
            command.DisplayName,
            passwordHasher.HashPassword(command.Password),
            now);
        var workspace = Workspace.Create(command.WorkspaceName, now);
        var membership = WorkspaceMember.Join(
            workspace.Id,
            user.Id,
            WorkspaceRole.Admin,
            now);

        await userRepository.AddAsync(user, cancellationToken);
        await workspaceRepository.AddAsync(workspace, cancellationToken);
        await workspaceMemberRepository.AddAsync(membership, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateResponse(user, workspace, membership, now);
    }

    public async Task<AuthenticationResponse> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await userRepository.GetByNormalizedEmailAsync(
            NormalizeEmail(command.Email),
            cancellationToken);
        var verificationStatus = passwordHasher.VerifyPassword(
            user?.PasswordHash,
            command.Password);

        if (user is null
            || !user.IsActive
            || verificationStatus == PasswordVerificationStatus.Failed)
        {
            throw new InvalidCredentialsException();
        }

        var memberships = await workspaceMemberRepository.ListByUserIdAsync(
            user.Id,
            cancellationToken);
        var membership = memberships.FirstOrDefault()
            ?? throw new ConflictException("The account is not assigned to a workspace.");
        var workspace = await workspaceRepository.GetByIdAsync(
            membership.WorkspaceId,
            cancellationToken)
            ?? throw new ConflictException("The account workspace could not be found.");
        var now = timeProvider.GetUtcNow();

        if (verificationStatus == PasswordVerificationStatus.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.HashPassword(command.Password), now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return CreateResponse(user, workspace, membership, now);
    }

    private AuthenticationResponse CreateResponse(
        User user,
        Workspace workspace,
        WorkspaceMember membership,
        DateTimeOffset issuedAt)
    {
        var accessToken = accessTokenGenerator.Generate(user, membership, issuedAt);

        return new AuthenticationResponse(
            accessToken.Value,
            "Bearer",
            accessToken.ExpiresAt,
            user.Id,
            user.Email,
            user.DisplayName,
            workspace.Id,
            workspace.Name,
            membership.Role.ToString());
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToUpperInvariant();
    }
}
