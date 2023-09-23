Building
========

This page has directions for building the documentation.

## Install the dependencies
```shell
$ pip install --user -U sphinx-mathjax-offline
$ pip install --user -U guzzle_sphinx_theme
```

## Build the HTML documentation
Run this in the doc directory:
```
$ sphinx-autobuild -t offline . _build/
```

## Build the Latex documentation
Run this in the doc directory:
```
$ make latexpdf
```
