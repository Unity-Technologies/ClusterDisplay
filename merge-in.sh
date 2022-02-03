if [ "$1" != "msg-merge" ] && [ "$1" != "msg-core" ]; then
	echo "Refusing to merge: \"$1\"."
	exit 1
fi

git pull origin $1 --squash

git reset -- templates
git reset -- source/com.unity.cluster-display.rpc
git reset -- source/com.unity.cluster-display.helpers
git reset -- source/com.unity.cluster-display.samples

if [ "$1" = "msg-merge" ]; then
	rm -r templates
	rm -r source/com.unity.cluster-display.rpc
	rm -r source/com.unity.cluster-display.helpers
	rm -r source/com.unity.cluster-display.samples
else
	git restore templates
	git restore source/com.unity.cluster-display.rpc
	git restore source/com.unity.cluster-display.helpers
	git restore source/com.unity.cluster-display.samples
fi
