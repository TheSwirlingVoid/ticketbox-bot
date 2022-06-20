using MongoDB.Bson;
using MongoDB.Driver;

class DocumentWithCollection {

	public IMongoCollection<BsonDocument> collection;
	public BsonDocument document;

	public DocumentWithCollection(IMongoCollection<BsonDocument> collection, BsonDocument document)
	{
		this.collection = collection;
		this.document = document;
	}
}