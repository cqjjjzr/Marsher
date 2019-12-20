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
            if (File.Exists(PortableSwitchFilename))
                return Path.Combine(parts);

            var path = Path.Combine(BasePath);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return Path.Combine(BasePath.Concat(parts).ToArray());
        }
    }

    public class QaDataContext : DbContext
    {
        public DbSet<QaItem> Items { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = MarsherFilesystem.GetPath("database.sqlite3") }.ConnectionString;
            var connection = new SqliteConnection(connectionString);

            optionsBuilder.UseLazyLoadingProxies().UseSqlite(connection);
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
            var directory = MarsherFilesystem.GetPath(ListDirectoryName);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!Path.HasExtension(file) || Path.GetExtension(file) != ExtensionName) continue;
                LoadList(file);
            }
        }

        public QaListStubs CreateList(string name, IList<QaItem> initialMembers = null)
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
            if (initialMembers != null)
                list.Items.AddRange(initialMembers.Select(it => it.Id));
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
            if (filenameChanged && stub.Filename != filename)
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

        public bool CheckValidListName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        public bool CheckDuplicateListName(string name, QaListStubs current = null)
        {
            if (_lists.Any(it => it.Name == name && !ReferenceEquals(it, current))) return true;
            if (current != null) return false;
            var filename = MarsherFilesystem.GetPath(ListDirectoryName, name + ExtensionName);
            if (!File.Exists(filename)) return false;
            LoadList(filename);
            return true;
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
