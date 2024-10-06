@echo off
magick convert EasyCA.jpg -resize 48x48   LockScreenLogo.scale-200.png
magick convert EasyCA.jpg -resize 88x88   Square44x44Logo.scale-200.png
magick convert EasyCA.jpg -resize 24x24   Square44x44Logo.targetsize-24_altform-unplated.png
magick convert EasyCA.jpg -resize 300x300 Square150x150Logo.scale-200.png
magick convert EasyCA.jpg -resize 50x50   StoreLogo.png
