using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.Users;

public record UserDto(Guid Id, string Email, string FullName, bool IsActive,
    DateTime? LastLoginAt, IReadOnlyList<string> Roles);

public record GetUsersQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<UserDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _users;

    public GetUsersQueryHandler(IUserRepository users) => _users = users;

    public async Task<PagedResult<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var (items, total) = await _users.PagedWithRolesAsync(request.Page, request.PageSize, ct);
        return new PagedResult<UserDto>(
            items.Select(u => new UserDto(u.Id, u.Email, u.FullName, u.IsActive, u.LastLoginAt,
                u.Roles.Select(r => r.Name).ToList())).ToList(),
            total, request.Page, request.PageSize);
    }
}

// ─────────────────── Restablecer contraseña (administrador) ───────────────────
// Nota de seguridad: las contraseñas se guardan hasheadas (una sola vía); no se pueden
// leer ni mostrar. El administrador solo puede ASIGNAR una nueva contraseña al usuario.

public record AdminResetPasswordCommand(Guid UserId, string NewPassword) : IRequest<Result<bool>>;

public class AdminResetPasswordCommandValidator : AbstractValidator<AdminResetPasswordCommand>
{
    public AdminResetPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10)
            .Matches("[A-Z]").WithMessage("La contraseña debe contener al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe contener al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe contener al menos un dígito.");
    }
}

public class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, Result<bool>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;

    public AdminResetPasswordCommandHandler(IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow)
    {
        _users = users;
        _hasher = hasher;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(AdminResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null) return Result<bool>.Failure("Usuario no encontrado.");

        user.ChangePassword(_hasher.Hash(request.NewPassword));
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────── Activar / desactivar / eliminar usuario (admin) ───────────────────

public record SetUserActiveCommand(Guid UserId, bool Active) : IRequest<Result<bool>>;

public class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand, Result<bool>>
{
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public SetUserActiveCommandHandler(IUserRepository users, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _users = users;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(SetUserActiveCommand request, CancellationToken ct)
    {
        if (!request.Active && _currentUser.UserId == request.UserId)
            return Result<bool>.Failure("No puedes desactivar tu propia cuenta.");

        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null) return Result<bool>.Failure("Usuario no encontrado.");

        if (request.Active) user.Activate(); else user.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public record DeleteUserCommand(Guid UserId) : IRequest<Result<bool>>;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public DeleteUserCommandHandler(IUserRepository users, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _users = users;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        if (_currentUser.UserId == request.UserId)
            return Result<bool>.Failure("No puedes eliminar tu propia cuenta.");

        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null) return Result<bool>.Failure("Usuario no encontrado.");

        user.IsDeleted = true;
        user.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public record UpdateUserRolesCommand(Guid UserId, List<string> Roles) : IRequest<Result<bool>>;

public class UpdateUserRolesCommandHandler : IRequestHandler<UpdateUserRolesCommand, Result<bool>>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;

    public UpdateUserRolesCommandHandler(IUserRepository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(UpdateUserRolesCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        foreach (var role in user.Roles.ToList())
            user.RemoveRole(role.Id);

        foreach (var roleName in request.Roles.Distinct())
        {
            var role = await _users.GetRoleByNameAsync(roleName, ct);
            if (role is null) return Result<bool>.Failure($"El rol '{roleName}' no existe.");
            user.AssignRole(role);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
