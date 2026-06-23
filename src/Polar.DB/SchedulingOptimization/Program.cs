using GenerationPersonExternalKeySample;

var options = new GenerationStoreOptions
{
    RootPath = Path.Combine(AppContext.BaseDirectory, "work", "persons-db"),

    // Для демонстрации поколение сразу считается устаревшим.
    // В реальном приложении поставьте TimeSpan.FromHours(6/24/etc).
    RotationInterval = TimeSpan.Zero
};

var manager = new PolarGenerationManager(options, PersonSchema.CreateRecipe());

manager.EnsureInitialGeneration(new[]
{
    PersonSchema.Create(1, "team-a", "Alice"),
    PersonSchema.Create(2, "team-a", "Bob"),
    PersonSchema.Create(3, "team-b", "Clara")
});

using (var active = manager.OpenActive())
{
    active.Sequence.AppendElement(PersonSchema.Create(4, "team-a", "Dmitry"));
    active.Sequence.AppendElement(PersonSchema.Tombstone(2));
    active.Sequence.Flush();
}

var scheduler = new GenerationRotationScheduler(manager, TimeSpan.FromMinutes(1));
var rotated = scheduler.RunOnce();
Console.WriteLine(rotated ? "Создано новое поколение." : "Поколение ещё актуально.");

using var current = manager.OpenActive();
Console.WriteLine($"Активное поколение: {current.Id}");
Console.WriteLine("Живые записи по external_key = team-a:");

foreach (var person in PersonSchema.FindByExternalKey(current.Sequence, "team-a"))
{
    Console.WriteLine($"{PersonSchema.GetId(person)}: {PersonSchema.GetName(person)}");
}
