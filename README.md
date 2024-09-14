# EDI_KMC_Service

![SFL Service LLC](Favicon.ico)
[SFL Service LLC](https://www.sflservicellc.com)

This was buid as the KMC functionality within KMC does not process the files in a FIFO manner. 
This will hold the files in the initial folder and then move the oldest held file to the processing folder.
Once the file is processed it will moved the folwing oldest file from the holding folder to the processing folder.


**Upgrade Notes**
This will also delete any old parameters if you had the original version called EDIService. 
Simply stop and uninstall the previous version and install this one.
You will have to change the EDI_KMC_Service.config file to fit you SQL server and Database on the ESP Entities connection string.
