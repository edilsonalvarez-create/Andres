using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
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
        var (items, total) = await _users.PagedAsync(request.Page, request.PageSize, u => !u.IsDeleted, ct);
        return new PagedResult<UserDto>(
            items.Select(u => new UserDto(u.Id, u.Email, u.FullName, u.IsActive, u.LastLoginAt,
                u.Roles.Select(r => r.Name).ToList())).ToList(),
            total, request.Page, request.PageSize);
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
