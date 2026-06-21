using Polar.DB;
using Polar.DB.ExternalKey;
using Polar.DB.SchedulingOptimization;
using Polar.Universal;

public sealed class SchedulingOptimization
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string rootPath = $"{Path.GetTempPath()}\\data\\{Guid.NewGuid():N}\\";
        EpochFolderManager epochFolderManager = new EpochFolderManager(rootPath);

        using var activeSequence = epochFolderManager.OpenLastOrCreateNewEpoch((dbPath, isNew) => isNew
            ? CreateDb(dbPath, new[]
            {
                PersonSchema.Create(1, 1123, "Alice"),
                PersonSchema.Create(2, 5423, "Bob"),
                PersonSchema.Create(3, 5123, "Clara")
            })
            : PersonSchema.CreateOrOpen(dbPath));


        await Scheduler.RunAsync(cancellationToken1 => epochFolderManager.DoNewEpoch((newPath, isNew) =>
        {
            if (isNew)
            {
                var activeRows = activeSequence.ElementValues()
                    .Where(record => !PersonSchema.Deleted(record))
                    .ToArray();
                return CreateDb(newPath, activeRows);
            }
            else
            {
               return PersonSchema.CreateOrOpen(newPath);
            }
        }), TimeSpan.FromMinutes(5), cancellationToken);
        
        activeSequence.AppendElement(PersonSchema.Tombstone(1));
        
        foreach (var person in activeSequence.ElementValues())
        {
            Console.WriteLine($"{PersonSchema.GetId(person)}: {PersonSchema.GetName(person)}: {PersonSchema.Deleted(person)}");
        }
    }

    /// <summary>
    /// Создаёт БД или открывает существующую, добавляет строки.
    /// </summary>
    /// <param name="dbPath">путь где создаётся или уже лежат файлы БД</param>
    /// <param name="rows"></param>
    /// <returns>созданная или открытая БД</returns>
    public USequence CreateDb(string dbPath, IEnumerable<object> rows)
    {
        var sequence = PersonSchema.CreateOrOpen(dbPath);
        sequence.Load(rows);
        sequence.Flush();
        sequence.Build();
        return sequence;
    }
}