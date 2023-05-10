#!/bin/bash

# Get the version number from the file
version=$(cat ../Version)

# Check if version is not empty
if [ ! -z "$version" ]; then
    # Build the project and redirect only the error output
    dotnet build ../src --configuration Release /p:Version=$version /p:Force=true 2> build_errors.txt

    # Check if the error output is empty
    # ...
else
    echo "Error: Failed to retrieve the version number from the file."
fi

# Check if the error output is empty
if [ ! -s build_errors.txt ]; then
    echo "Compilation succeeded, creating zip file..."
    
    # Delete existing zip file if it exists
    if [ -e "../dist/Executor_$version.zip" ]; then
        rm "../dist/Executor_$version.zip"
    fi

    # Zip the output files
    mkdir -p ../dist
    zip -r "../dist/Executor_$version.zip" "../src/bin/Release/net6.0"
    echo "Zip file created successfully."

    # Clean the bin and obj folders
    echo "Cleaning up the bin and obj folders..."
    rm -rf "../src/bin"
    rm -rf "../src/obj"
    echo "Clean-up completed."

else
    echo "Compilation failed. Please check the build_errors.txt for more information."
fi

# Clean up
rm build_errors.txt
