#!/bin/bash
ROOT=$(dirname $0)
if [ $# -ne 0 ]; then
  echo "Usage: $0" >&2
  echo "Formats the source code files using clang-format." >&2
  exit 1
fi
clang-format -i "$ROOT/src/"*.cs "$ROOT/test/"*.cs "$ROOT/test/util/"*.cs
