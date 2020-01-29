using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Marsher
{
    public class ImportSession
    {
        private readonly QaDataContext _dataContext;
        private readonly LocalListPersistence _persistence;

        private readonly ImportExportData _data;

        public ImportSession(string filename, QaDataContext dataContext, LocalListPersistence persistence)
        {
            _dataContext = dataContext;
            _persistence = persistence;

            var str = File.ReadAllText(filename, Encoding.UTF8);
            _data = JsonConvert.DeserializeObject<ImportExportData>(str);
            if (_data.items == null) throw new ArgumentException();
        }

        public int ItemsCount => _data.items.Count;

        private void ImportToDatabase()
        {
            var dataContextItems = _dataContext.Items;
            foreach (var item in _data.items)
            {
                var existing = dataContextItems.Find(item.Id);
                if (existing == null)
                {
                    dataContextItems.Add(item);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existing.Translation) ||
                    !string.IsNullOrWhiteSpace(item.Translation)) continue;
                item.Translation = existing.Translation;
                dataContextItems.Update(item);
            }
        }

        public void ImportToExisting(QaListStubsViewModel list)
        {
            ImportToDatabase();

            if (list == null) return;
            var items = list.PopulatedItems;
            foreach (var item in _data.items.Where(item => !items.Contains(item)))
                items.Add(item);
        }

        public void ImportToNew(string listName)
        {
            ImportToDatabase();

            _persistence.CreateList(listName, _data.items);
        }
    }

    public class ExportSession
    {
        public readonly string Json;

        public ExportSession(IList<QaItem> items)
        {
            var data = new ImportExportData
            {
                items = new List<QaItem>(items)
            };
            Json = JsonConvert.SerializeObject(data);
        }

        public void ExportToFile(string filename)
        {
            File.WriteAllText(filename, Json, Encoding.UTF8);
        }
    }
    public class ImportExportData
    {
        public List<QaItem> items = new List<QaItem>();
    }
}
