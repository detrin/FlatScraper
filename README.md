
#  FlatScraper 
.NET app for scraping flat offers into MongoDB database, currently works with sreality.cz site.
The purpose of this app is to run over all listed flat offers, check them in the database and on change update data in the database.


Before running the app, add into appsettings.json your secret connect for MongoDB.
```json
{
    "Database": {
        "useSecret": "secretLocal",
        "secretLocal": "mongodb://127.0.0.1:27017/?gssapiServiceName=mongodb",
        "secret": "COPY_HERE_YOUR_CONNECT_LINK"
    },
    "workersCount": 5
}
```