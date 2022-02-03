git pull origin msg-merge --squash

git reset -- templates
git reset -- source/com.unity.cluster-display.rpc
git reset -- source/com.unity.cluster-display.helpers
git reset -- source/com.unity.cluster-display.samples

rm -r templates
rm -r source/com.unity.cluster-display.rpc
rm -r source/com.unity.cluster-display.helpers
rm -r source/com.unity.cluster-display.samples

git commit -m "Merging in msg-merge"
