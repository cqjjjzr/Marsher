using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marsher
{
    public class QaDataContext : DbContext
    {
        public DbSet<QaItem> Items { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = "database.sqlite3" }.ConnectionString;
            var connection = new SqliteConnection(connectionString);

            optionsBuilder.UseSqlite(connection);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<QaItem>()
                .HasIndex(i => i.Id)
                .IsUnique();
        }

        public QaList LoadStubs(QaListStubs stubs)
        {
            var list = new QaList {Name = stubs.Name};
            list.Items.AddRange(stubs.Items.Select(GetItemById).Where(it => it != null));
            return list;
        }

        public QaItem GetItemById(string id)
        {
            return Items.SingleOrDefault(item => item.Id == id);
        }
    }

    public class LocalListPersistence
    {
        private const string ListDirectoryName = "lists";
        private const string ExtensionName = ".txt";

        private List<QaListStubs> lists = new List<QaListStubs>();
        private QaDataContext _database;

        public LocalListPersistence(QaDataContext database)
        {
            _database = database;
            if (!Directory.Exists(ListDirectoryName))
                Directory.CreateDirectory(ListDirectoryName);
            foreach (var file in Directory.EnumerateFiles(ListDirectoryName))
            {
                if (!Path.HasExtension(file) || Path.GetExtension(file) != ExtensionName) continue;
                LoadList(file);
            }
        }

        public QaListStubs CreateList(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new IllegalListNameException();
            if (lists.Any(it => it.Name == name)) throw new DuplicateListNameException();
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new IllegalListNameException();

            var filename = Path.Combine(ListDirectoryName, name + ExtensionName);
            if (File.Exists(filename))
            {
                LoadList(filename);
                throw new DuplicateListNameException();
            }

            try
            {
                File.Create(filename).Close();
            }
            catch (IOException)
            {
                throw new IllegalListNameException();
            }
            var list = new QaListStubs()
            {
                Name = name,
                Filename = filename
            };
            lists.Add(list);
            OnListModified?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list));
            return list;
        }

        public void LoadList(string file)
        {
            var list = new QaListStubs()
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Filename = file
            };
            list.Items.AddRange(File.ReadAllLines(file));
            lists.Add(list);
            OnListModified?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list));
        }

        public void UpdateList(QaListStubs stub, bool filenameChanged = false)
        {
            var filename = Path.Combine(ListDirectoryName, stub.Name + ExtensionName);
            if (filenameChanged)
            {
                if (string.IsNullOrWhiteSpace(stub.Name)) throw new IllegalListNameException();
                if (lists.Any(it => it.Name == stub.Name && it != stub)) throw new DuplicateListNameException();
                if (stub.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new IllegalListNameException();
                try
                {
                    File.Move(stub.Filename, filename);
                }
                catch (FileNotFoundException)
                {
                    // Ignored
                }
                catch (IOException)
                {
                    throw new IllegalListNameException();
                }
                stub.Filename = filename;
            }

            File.WriteAllLines(filename, stub.Items);

            OnListModified?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, stub, stub));
        }

        public void RemoveList(QaListStubs stub)
        {
            lists.Remove(stub);
            OnListModified?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, stub));
            File.Delete(stub.Filename);
        }

        public IEnumerable<QaListStubs> GetAllStubs()
        {
            return lists;
        }

        public event NotifyCollectionChangedEventHandler OnListModified;
    }

    public class IllegalListNameException : Exception
    {

    }

    public class DuplicateListNameException : Exception
    {

    }
}
