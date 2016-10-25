# IoT Azure Functions HoLs

## Introduction

### Welcome to the Hands on Lab at Microsoft Ignite 2016.

#### What is IoT? 

The Internet of Things (IoT) refers to the ever-growing network of physical objects that feature an IP address for internet connectivity, and the communication that occurs between these objects and other Internet-enabled devices and systems.

The Internet of Things extends internet connectivity beyond traditional devices like desktop and laptop computers, smartphones and tablets to a diverse range of devices and everyday things that utilise embedded technology to communicate and interact with the external environment, all via the Internet.

### What will we be doing today?

 1. You will build a Windows 10 Application and deploy it to a Raspberry Pi 3, this application will automatically capture a picture of faces
 2. This image will be sent to your Azure Function 
 3. The Azure Function App will use Microsoft Cognitive Services **Vision API** which will take in a picture as the input and return the uploaded picture with a rectangle around the face detected 
 4. This response will be an image that would have detected one or more human faces in your image with face rectangles for where in the image the faces are, along with face attributes which contain machine learning-based predictions of facial features. After detecting faces, you can take the face rectangle and pass it to the Emotion API to speed up processing. The face attribute features available are: Age, Gender, Pose, Smile, and Facial Hair along with 27 landmarks for each face in the image. 
 
### Extra for experts 
 * We will be using the Vision API to detect faces, but the face API can also be used to expand this application to easily find similar-looking faces.
   * Given a collection of faces and a new face as a query, this API will return a collection of similar faces.

### What are Microsoft Cognitive Services?

Microsoft Cognitive Services let you build apps with powerful algorithms using just a few lines of code. They work across devices and platforms such as iOS, Android, and Windows, keep improving, and are easy to set up. 


## Learning Outcomes
* Learn to build Windows 10 Application and deploy it to a Raspberry Pi 3 
* Learn the basics of IoT (Internet of Things)
* Learn about Microsoft Cognitive services

### Tools
* [Visual Studio Code](https://code.visualstudio.com)
* [Azure Portal](portal.azure.com) 

### Extra Resources
* [Microsoft Cognitive services](https://azure.microsoft.com/en-in/documentation/articles/app-service-web-overview/)
* [Windows 10 IoT core](https://developer.microsoft.com/en-us/windows/iot/explore/iotcore)
* [Microsoft IoT](http://www.microsoft.com/en-us/cloud-platform/internet-of-things)

### You have been given a:
* Raspberry Pi 3 with Windows 10 IoT core pre-installed
* Microsoft LifeCam HD-3000 Webcam
* Keyboard
* Mouse

## Download Postman to test your Azure Function - https://www.getpostman.com/apps
* Copy the URL from the functions app on the top.
* Open postman to make a Post request
* With this URL and in the Body attach an image that you can download from the internet

## Downloading Source Code from GitHub
To quickly download a project from Github, simply:
  * Navigate to the [repo link](https://github.com/MS-Ignite-NZ-2016/IoT-Azure-Functions-HoLs)
  * Click on **Download ZIP** on the mid-right section
     * Get the project [ZIP File](https://github.com/MS-Ignite-NZ-2016/IoT-Azure-Functions-HoLs/archive/master.zip) directly 
  * Save and extract the archive in a suitable location