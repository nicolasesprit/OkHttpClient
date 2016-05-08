MDTOOL ?= /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool

.PHONY: all clean

all: OkHttpClient.dll

package: OkHttpClient.dll
	mono vendor/nuget/NuGet.exe pack ./OkHttpClient.nuspec
	mv okhttpclient*.nupkg ./build/

OkHttpClient.dll: 
	$(MDTOOL) build -c:Release ./src/OkHttpClient/OkHttpClient.csproj
	mkdir -p ./build/MonoAndroid
	mv ./src/OkHttpClient/bin/Release/MonoAndroid/Ok* ./build/MonoAndroid

clean:
	$(MDTOOL) build -t:Clean OkHttpClient.sln
	rm *.dll
	rm -rf build
