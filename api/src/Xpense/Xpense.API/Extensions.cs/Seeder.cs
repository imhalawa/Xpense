using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xpense.Persistence;
using Xpense.Services.Abstract.Entities;

namespace Xpense.API.Extensions.cs
{
    public static class Seeder
    {
        public static void Seed<T>(XpenseDbContext context, string fileName) where T : BaseEntity, IEquatable<T>
        {
            var data = LoadData<T>(fileName).ToArray();
            var dbSet = context.Set<T>();

            var dataCount = data.Count();
            var dbSetCount = dbSet.Count();

            // TODO: Not an efficient way to detect the Actual Data, but still would do the trick 
            // Because it should run only whenever the application is starting for the first time
            if (dbSetCount >= dataCount)
                return;

            if (dbSetCount < dataCount)
            {
                var existingData = dbSet.ToList();
                var missingData = data.Where(d => !existingData.Contains(d));
                dbSet.AddRange(missingData);
            }
            else
            {
                dbSet.AddRange(data);
            }

            var result = context.SaveChanges();

            if (result < 1)
                throw new InvalidOperationException($"Unable to commit seeds of {nameof(T)} to the database");

        }

        private static IEnumerable<T> LoadData<T>(string fileName) where T : BaseEntity, IEquatable<T>
        {
            var jsonFile = File.ReadAllText(@"Seeds" + Path.DirectorySeparatorChar + fileName);
            if (string.IsNullOrEmpty(jsonFile))
                throw new InvalidOperationException($"Cannot seed {nameof(T)}, the seeding file is empty.");

            var data = Parse<T>(jsonFile);

            data = data.Select(p =>
            {
                p.CreatedOn = DateTime.Now;
                return p;
            });

            return data;
        }

        private static IEnumerable<T> Parse<T>(string json) where T : BaseEntity, IEquatable<T>
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var prioritiesElement = root.GetProperty("priorities");
            return JsonSerializer.Deserialize<T[]>(prioritiesElement.GetRawText())!;
        }
    }
}
