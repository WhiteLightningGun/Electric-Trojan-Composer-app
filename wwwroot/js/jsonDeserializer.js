// @ts-check

let url = '/Ajax/JsonGetOne';
let jsonResult;

const fetchPromise = fetch(url);
  
fetchPromise.then((response) => {
    const jsonPromise = response.json();

    jsonPromise.then((data) => {
        console.log(data[0].artist);
        jsonResult = data;
        console.log(jsonResult);
    });
  });

function summary()
{
    console.log("click was clicked");
    console.log(`after parsing, jsonResult has filePath: ${jsonResult[0].filePath}, songName: ${jsonResult[0].songName}, artist:${jsonResult[0].artist}`);
}

const fff = [];


