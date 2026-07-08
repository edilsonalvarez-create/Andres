using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.Auth;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresInMinutes,
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

// ─────────────────────────────── Login ───────────────────────────────

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponseDto>>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _uow;

    public LoginCommandHandler(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt, IUnitOfWork uow)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _uow = uow;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);
        // Mensaje genérico: no revelar si el correo existe (OWASP A07).
        const string invalidCredentials = "Credenciales inválidas.";

        if (user is null || !user.IsActive)
            return Result<AuthResponseDto>.Failure(invalidCredentials);

        if (user.IsLocked)
            return Result<AuthResponseDto>.Failure("Cuenta bloqueada temporalmente por intentos fallidos. Intente más tarde.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin();
            await _uow.SaveChangesAsync(ct);
            return Result<AuthResponseDto>.Failure(invalidCredentials);
        }

        user.RegisterSuccessfulLogin();
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();
        user.AddRefreshToken(refreshToken, DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays));
        await _uow.SaveChangesAsync(ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            accessToken, refreshToken, _jwt.AccessTokenMinutes,
            user.Id, user.Email, user.FullName,
            user.Roles.Select(r => r.Name).ToList()));
    }
}

// ─────────────────────────── Refresh Token ───────────────────────────

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponseDto>>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _uow;

    public RefreshTokenCommandHandler(IUserRepository users, IJwtTokenService jwt, IUnitOfWork uow)
    {
        _users = users;
        _jwt = jwt;
        _uow = uow;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await _users.GetByRefreshTokenAsync(request.RefreshToken, ct);
        var token = user?.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);

        if (user is null || token is null || !token.IsActive)
            return Result<AuthResponseDto>.Failure("Token de refresco inválido o expirado.");

        token.Revoke(); // Rotación de refresh tokens
        var accessToken = _jwt.GenerateAccessToken(user);
        var newRefreshToken = _jwt.GenerateRefreshToken();
        user.AddRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays));
        await _uow.SaveChangesAsync(ct);

        return Result<AuthResponseDto>.Success(new AuthResponseDto(
            accessToken, newRefreshToken, _jwt.AccessTokenMinutes,
            user.Id, user.Email, user.FullName,
            user.Roles.Select(r => r.Name).ToList()));
    }
}

// ─────────────────────────── Registrar usuario ───────────────────────────

public record RegisterUserCommand(string Email, string FullName, string Password, List<string> Roles)
    : IRequest<Result<Guid>>;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        // Política de contraseñas OWASP: longitud mínima 10, mayúscula, minúscula, dígito.
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10)
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un dígito.");
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Debe asignar al menos un rol.")
            .ForEach(r => r.Must(role => SystemRoles.All.Contains(role))
                .WithMessage(role => $"Rol desconocido."));
    }
}

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<Guid>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;

    public RegisterUserCommandHandler(IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow)
    {
        _users = users;
        _hasher = hasher;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var existing = await _users.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            return Result<Guid>.Failure("Ya existe un usuario registrado con ese correo.");

        var user = new User(request.Email, request.FullName, _hasher.Hash(request.Password));
        foreach (var roleName in request.Roles.Distinct())
        {
            var role = await _users.GetRoleByNameAsync(roleName, ct);
            if (role is null)
                return Result<Guid>.Failure($"El rol '{roleName}' no existe.");
            user.AssignRole(role);
        }

        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(user.Id);
    }
}
