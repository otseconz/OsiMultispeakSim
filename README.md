# OSI Multispeak Simulator

The purpose of this project is to develop a Multispeak server that can simulate a metering network, and can integrate with OSI Monarch Electra OMS for testing and development purposes.

Built on **.NET 10** with **CoreWCF** and **ASP.NET Core**.

Fair warning: This is a rapid prototype, please direct code complaints to the AI.

## Features

The server implements part of the **OMS MultiSpeak v4.1** specification ( `OMS_Multispeak_v41.wsdl`), the following features are supported:
- Multispeak Methods:
  - `PingURL`, `GetMethods`
  - `InitiateOutageDetectionEventRequest`, `ODEventNotification`
  - `InitiateMeterReadingsByMeterID`, `ReadingChangedNotification`
  - `MeterEventNotification`
- **In-memory meter store**: virtual meters that store Power status, Communication status, and a list of measurements.  Meter store can be populated from an OSI Premise.xml import file, or updated via the REST API.
- **Web UI**: manage meters and send unsolicited messages to OMS.

![Server UI](doc/server-ui.png)

## Configuration

In `appsettings.json`, configure the OMS server and multispeak path, e.g.

```json
{
  "MultiSpeakClient": {
    "BaseUrl": "http://192.168.0.90:8080",
    "MultiSpeakPath": "/axis2/services/OMS_MultiSpeak_v41.OMS_MultiSpeak_v41_Soap11_Endpoint/"
  }
}
```
Configure the OMS Multispeak Username\Password:

```bash
cd Multispeak.Server
dotnet user-secrets set "MultiSpeakClient:Username": "admin",
dotnet user-secrets set "MultiSpeakClient:Password": "admin"
```
## Run

```bash
cd Multispeak.Server
dotnet run
```

## Basic Usage

Default endpoints:
- **Simulation UI**: https://localhost:58309 http://localhost:58310/
- **API**: https://localhost:58309/swagger http://localhost:58310/swagger
- **SOAP endpoint**: https://localhost:58309/Multispeak http://localhost:58310/Multispeak  (SOAP 1.1)
- **SOAP endpoint**: https://localhost:58309/Multispeak12 http://localhost:58310/Multispeak12 (SOAP 1.2)


The Meter Store can be populated manually by using the UI, from an OMS Premises XML file, or via the REST API. The Meter Store can be persisted by using the Save database function, or via the REST API.  The store is written to meters.json in the project folder.

Use the Simuation UI to edit meter data, and send unsolicited messages to OMS.

## REST API
There is a Rest API for interacting with the Meter Store and sending unsolicited commands to OMS.  Refer to Swagger for details.

![Swagger](doc/swagger.png)
## OSI OMS Setup
A minimum viable OMS Multispeak configuration is summarized follows:
![Server UI](doc/oms-config.png)

PremiseServiceType objects must have the Meter ID and AMI System ID set before they will be used with AMI integration.  Set these values via the OMS API, or via Premise XML import.

## Methods

TODO

## Roadmap
- Tidy up error handling and logging
- TLS configuration and certificate management
- Add callbacks and events to allow integration with other services, such as third party APIs
- Customizable meter reading fields
