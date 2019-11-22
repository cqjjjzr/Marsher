using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marsher
{
    public static class MarsherFilesystem
    {
        private const string PortableSwitchFilename = "portable_version";
        private static readonly string[] BasePath = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Marsher"
        };

        public static string GetPath(params string[] parts)
        {
            return Path.Combine(
                File.Exists(PortableSwitchFilename)
                    ? parts : BasePath.Concat(parts).ToArray());
        }
    }

    public class QaDataContext : DbContext
    {
        public DbSet<QaItem> Items { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = MarsherFilesystem.GetPath("database.sqlite3") }.ConnectionString;
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

        private readonly List<QaListStubs> _lists = new List<QaListStubs>();

        public LocalListPersistence()
        {
            if (!Directory.Exists(MarsherFilesystem.GetPath(ListDirectoryName)))
                Directory.CreateDirectory(MarsherFilesystem.GetPath(ListDirectoryName));
            foreach (var file in Directory.EnumerateFiles(ListDirectoryName))
            {
                if (!Path.HasExtension(file) || Path.GetExtension(file) != ExtensionName) continue;
                LoadList(file);
            }
        }

        public QaListStubs CreateList(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new IllegalListNameException();
            if (_lists.Any(it => it.Name == name)) throw new DuplicateListNameException();
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new IllegalListNameException();

            var filename = MarsherFilesystem.GetPath(ListDirectoryName, name + ExtensionName);
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
            _lists.Add(list);
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
            _lists.Add(list);
            OnListModified?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list));
        }

        public void UpdateList(QaListStubs stub, bool filenameChanged = false)
        {
            var filename = MarsherFilesystem.GetPath(ListDirectoryName, stub.Name + ExtensionName);
            if (filenameChanged)
            {
                if (string.IsNullOrWhiteSpace(stub.Name)) throw new IllegalListNameException();
                if (_lists.Any(it => it.Name == stub.Name && it != stub)) throw new DuplicateListNameException();
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
            _lists.Remove(stub);
            OnListModified?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, stub));
            File.Delete(stub.Filename);
        }

        public IEnumerable<QaListStubs> GetAllStubs()
        {
            return _lists;
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
