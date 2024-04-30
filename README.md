# RocketCsv
A very fast CSV parser. Tons of features and a very easy to configure fluent API.

# Configuration
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

You simply creating a csv map based off of CsvMapBase<T> and mark it with the [CsvMap] attribute.

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

# Usage

```
            using var mmf = MemoryMappedFile.CreateFromFile(@"CsvFiles\customers-100_TooFew.csv", FileMode.Open);
            using var csvReader = new CsvReader(mmf.CreateViewStream());

            var userCsvMap = new UserCsvMap(csvReader);

            await UserCsvMap.ReadHeaderRowAsync();
            var customers = await UserCsvMap.ReadDataRowsAsync();
```
