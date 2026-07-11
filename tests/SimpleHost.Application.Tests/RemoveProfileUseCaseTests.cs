using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.RemoveProfile;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.Tests;

public class RemoveProfileUseCaseTests
{
    [Fact]
    public void Removes_the_named_profile_through_the_store()
    {
        var store = new RecordingProfileStore();
        var useCase = new RemoveProfileUseCase(store);

        useCase.Execute("Work");

        store.Removed.ShouldBe(new[] { "Work" });
    }

    private sealed class RecordingProfileStore : IProfileStore
    {
        public List<string> Removed { get; } = [];

        public void Remove(string name) => Removed.Add(name);

        public void Save(Profile profile) => throw new NotSupportedException();

        public Profile? Find(string name) => throw new NotSupportedException();

        public IReadOnlyList<Profile> GetAll() => throw new NotSupportedException();
    }
}
