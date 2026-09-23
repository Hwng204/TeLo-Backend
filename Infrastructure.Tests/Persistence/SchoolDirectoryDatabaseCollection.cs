namespace Infrastructure.Tests.Persistence;

// The directory/import integration tests share one MySQL database and clean up with wide DELETEs
// (id ranges OR code prefixes). Run in parallel they take conflicting locks and deadlock, so every
// class that touches those tables joins this collection and runs one after another.
[CollectionDefinition(Name)]
public sealed class SchoolDirectoryDatabaseCollection
{
    public const string Name = "SchoolDirectoryDatabase";
}
