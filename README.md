# SBC Invoice scraper

A rough .NET Core Console project to scrape invoice information from SBC / MediusFlow site.
Intended to generate files/data for manual consumption mostly, easily searchable by common tools when stored on file system or e.g. Google Drive.

## Get started
### Modify configuration
#### appsettings.json
  * UserLoginId_BankId
    * your "personnummer"
  * MediusRequestHeader_XUserContext
    * content of x-user-context header (find out from network reequests when manually using SBC / MediusFlow)
  * LoginPage_BankId etc
    * modify if necessary

*Consider using .NET's secrets manager to store sensitive information. Values in secrets.json will override appsettings.json.*


### Using the program
Start the program. A wild console appears. Run  
```> init```  
This opens Chrome and takes you to the login page (as defined by ```LoginPage_BankId```) so you can authorize using BankID.  **TODO: PID as optional argument to login**  
The browser then redirects to the ```RedirectUrlMediusFlow``` page. 

Once loaded, run  

```> scrape [yyyy-MM-dd]```  
to begin scraping. **TODO: start/end date, reverse**

Invoice json files will be saved in tje ```StorageFolderRoot``` folder.
Downloaded invoice images (not enabled by default) will be saved in the ```StorageFolderDownloadedFiles``` folder.

Semicolon can be used to separate commands, e.g.
```> init; scrape; createindex > file.csv``` will start the scraping once initialization is done, then store the index to file.

```> createindex```  
Reads the json files and creates a csv with the most important information. In order to save as file, use a pipe, e.g.  
```> createindex > index.csv```  

## TODOs
* Fix the organically grown code structure 
* Unit tests
* Better DTOs (these were generated by [quicktype.io](https://app.quicktype.io/#l=cs&r=json2csharp) - great to get started quickly, but not suited for release)
* Identify all data which might be of interest (more endpoints to call?) and strip everything else. The MediusFlow DTOs are incredibly large, with low information density
* Image handling 
  * use filenames with more info (currently only a GUID) so they can be found manually
  * Some image processing (b/w, resize) for smaller footprint
