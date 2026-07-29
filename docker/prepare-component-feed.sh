#!/bin/sh
set -eu

package_version="${1:?component package version is required}"
component_root="/src/CanDoItAll.Components"
feed_root="/src/CanDoItAll/ExternalPackages"

mkdir -p "$feed_root"

for project in \
    CanDoItAll.Components.Common \
    CanDoItAll.Components.BaseLib \
    CanDoItAll.Components.Charts \
    CanDoItAll.Components.Mermaid \
    CanDoItAll.Components.QRCode
do
    dotnet pack \
        "$component_root/src/$project/$project.csproj" \
        --configuration Release \
        --output "$feed_root" \
        /m:1 \
        /p:CanDoItAllPackageBaseVersion="$package_version" \
        /p:Version="$package_version" \
        /p:PackageVersion="$package_version"
done
