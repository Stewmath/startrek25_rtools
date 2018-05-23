# Use this makefile to recompress a directory of dumped files from Star Trek: 25th
# Anniversary. See the readme for details.

EXE = mono --debug ../startrek25_rtools/bin/Debug/startrek25_rtools.exe
PATCH_DIR = TREKCD/patches/

STAGE_FILES = $(wildcard files/*)
STAGE_FILES := $(foreach file, $(STAGE_FILES), $(PATCH_DIR)/$(notdir $(file)))

all: $(STAGE_FILES)

$(PATCH_DIR)/%: files/%
	$(EXE) --compressfile "$<" "$@"
