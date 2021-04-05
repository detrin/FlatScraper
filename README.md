
#  FlatScraper 
.NET app for scraping flat offers into MongoDB database 


Before running the app, add appsettings.json to local folder.
```json
{
    "Database": {
        "useSecret": "secretLocal",
        "secretLocal": "mongodb://127.0.0.1:27017/?gssapiServiceName=mongodb",
        "secret": "COPY_HERE_YOUR_CONNECT_LINK"
    }
}
```