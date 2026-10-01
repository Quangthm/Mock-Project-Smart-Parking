using UserService.Application.Common.Interfaces.Persistence;
using SmartParking.UserService.Domain.Entities;
using UserService.Persistence.FakeDatabase;
using UserService.Persistence.Repositories.BaseRepository;

namespace UserService.Persistence.Repositories;

public class UserRepository
    : GenericRepository<User>,
      IUserRepository
{
    public UserRepository()
        : base(InMemoryDatabase.Users)
    {
    }
}