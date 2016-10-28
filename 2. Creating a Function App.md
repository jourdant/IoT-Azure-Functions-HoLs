# IoT Azure Functions HoLs

# Getting started

## 1. Lets Begin
  * Go to [portal.azure.com](https://portal.azure.com)
  * Log in with the azure credentials 

## 2. Creating an Azure Funtion 
  * Click on "New" in the top left corner of the portals home page
  * Select "Virtual Machines"
  * Select "Function App"

## 3. Enter Details
  * Fill out the information about your Function App 
    - Note you should get a tick next to your **App name**
    - Try and add unique details, by adding your initials or name at the end of the app
  * For the "Resource Group", **Create** new Resource Group
  * Select any **Location**
  * Memory Allocation (MB) should be **256MB**
  * Click on **Create** at the bottom  

![New Function App](img/functionApp.png)

## 4. Azure is now deploying the Function App
    * Once done, create a new function with an **HTTP Trigger and C#**, like below:

![Faster Way](img/fasterWay.png)

## 5. Replace and Paste this code:

```csharp
#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#r "System.Drawing"
 
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO; 
using System.Drawing;
using System.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

 
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    var image = await req.Content.ReadAsStreamAsync();
    log.Info(Environment.GetEnvironmentVariable("Vision_API_Subscription_Key"));
    MemoryStream mem = new MemoryStream();
    image.CopyTo(mem); //make a copy since one gets destroy in the other API. Lame, I know.
    image.Position = 0;
    mem.Position = 0;
    log.Info("hello"); 
    string result = await CallVisionAPI(log, image); 
    log.Info(result); 

    VisionObject v = JsonConvert.DeserializeObject<VisionObject>(result);
    log.Info(v.description.captions[0].text);
    if (String.IsNullOrEmpty(result)) {
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }
     
    ImageData imageData = JsonConvert.DeserializeObject<ImageData>(result);
 
    MemoryStream outputStream = new MemoryStream();
    using(Image maybeFace = Image.FromStream(mem, true))
    {
        using (Graphics g = Graphics.FromImage(maybeFace))
        {
            Pen yellowPen = new Pen(Color.Yellow, 4);
            foreach (Face face in imageData.Faces)
            {
                var faceRectangle = face.FaceRectangle;
                g.DrawRectangle(yellowPen, 
                    faceRectangle.Left, faceRectangle.Top, 
                    faceRectangle.Width, faceRectangle.Height);
            }
        }
        maybeFace.Save(outputStream, ImageFormat.Jpeg);
    }
     
    var response = new HttpResponseMessage()
    {
        Content = new ByteArrayContent(outputStream.ToArray()),
        StatusCode = HttpStatusCode.OK,
    };
    response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
    return response;
}
 
static async Task<string> CallVisionAPI(TraceWriter log, Stream image)
{
    using (var client = new HttpClient())
    {
        var content = new StreamContent(image);
log.Info("1");
        var url = "https://api.projectoxford.ai/vision/v1.0/analyze?visualFeatures=Tags,Description,Faces";
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Environment.GetEnvironmentVariable("Vision_API_Subscription_Key"));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var httpResponse = await client.PostAsync(url, content);
log.Info("2");
log.Info(httpResponse.StatusCode.ToString());
        if (httpResponse.StatusCode == HttpStatusCode.OK){
            return await httpResponse.Content.ReadAsStringAsync();
        }
    }
    return null;
}
 
public class ImageData {
    public List<Face> Faces { get; set; }
}
 
public class Face {
    public int Age { get; set; }
    public string Gender { get; set; }
    public FaceRectangle FaceRectangle { get; set; }
}
 
public class FaceRectangle : TableEntity {
    public string ImageFile { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}


public class Tag
{
    public string name { get; set; }
    public double confidence { get; set; }
}

public class Caption
{
    public string text { get; set; }
    public double confidence { get; set; }
}

public class Description
{
    public List<string> tags { get; set; }
    public List<Caption> captions { get; set; }
}

public class Metadata
{
    public int width { get; set; }
    public int height { get; set; }
    public string format { get; set; }
}

public class VisionObject
{
    public List<Tag> tags { get; set; }
    public Description description { get; set; }
    public string requestId { get; set; }
    public Metadata metadata { get; set; }
    public List<Face> faces { get; set; }
}
```

## 6. Save

## 7. Cognitive services
 * Go to the Microsoft Cognitive Services to [Subscribe](https://www.microsoft.com/cognitive-services/en-us/subscriptions?productId=/products/54d873dd5eefd00dc474a0f4) for the free tier of Computer Vision API and Copy the Key, like below:

![New Web App](img/compVisionKey.png)

## 8. Adding API keys to Function app 
 * Go back to Functions app, and click on Functions App Settings
 * Go to App Service Settings
 * Click on Application Settings
 * Add a new Key 'Vision_API_Subscription_Key' and your Computer Vision API Key in the Value field, like below

![New Web App](img/appSettings.jpg)

#### Congratulations you have successfully created the Funtions App and Windows 10 IoT Application