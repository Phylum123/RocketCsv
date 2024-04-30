
<img src="https://github.com/Phylum123/RocketCsv/assets/16786358/7691f5c7-94b4-4640-86a5-711e9e7ee061" width="210">

A very fast CSV parser. Tons of features and a very easy to configure fluent API.

# Basic Configuration
You need to create a map from the class you want to populate and the format of the csv file. Give the class below:

```
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
    public string Email { get; set; }
}
```

And a csv file that looks like this:

```
id, name, location, email
0, "User0", "Location0", "Email0@email.com"
1, "User1", "Location1", "Email1@email.com"
2, "User2", "Location2", "Email2@email.com"
3, "User3", "Location3", "Email3@email.com"

```

You simply create a csv map by inheriting from CsvMapBase<T> and marking it with the [CsvMap] attribute.

```
    [CsvMap]
    public partial class UserCsvMap : CsvMapBase<User>
    {
        //This method is never actually called, but is used to generate the CSV mapping code.
        public void Configure(ICsvMapBuilder<User> builder)
        {
            builder
                .AutoMapByName(StringComparison.OrdinalIgnoreCase);
        }
    }
```

# Basic Usage

```
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\users-100.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var userCsvMap = new UserCsvMap(csvReader);

            await UserCsvMap.ReadHeaderRowAsync().ConfigureAwait(false);
            var users = await UserCsvMap.ReadDataRowsAsync().ConfigureAwait(false);
```
