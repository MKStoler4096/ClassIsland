param($token, $version)
$APP = "HelloWRC/ClassIsland"

echo "Uploading AppCenter..."

appcenter distribute release --group Collaborators --token $token -a $APP -f ./out/ClassIsland.zip -b $version
