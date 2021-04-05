using System;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Generic;

namespace FlatScraper
{
    public class MongoCRUD
    {
        private IMongoDatabase db;

        public MongoCRUD(string connectionString, string database)
        {
            var client = new MongoClient(connectionString);
            db = client.GetDatabase(database);
        }

        public void InsertRecord<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            collection.InsertOne(record);
        }

        public List<T> LoadRecords<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(new BsonDocument()).ToList();
        }

        public T LoadRecordsById<T>(string table, string id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("Link", id);
            var query = collection.Find(filter);
            if (query.CountDocuments() == 0)
            {
                return default(T);
            } else {
                return collection.Find(filter).First();
            }
        }

        public void UpsertRecord<T>(string table, string id, T record)
        {
            var collection = db.GetCollection<T>(table);
            var result = collection.ReplaceOne(
                new BsonDocument("_id", id),
                record,
                new UpdateOptions { IsUpsert = true });
        }

        public void DeleteRecord<T>(string table, string id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("Id", id);
            collection.DeleteOne(filter);
        }
    }
}