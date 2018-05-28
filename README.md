This is a program which provides some tools for dumping and modifying files in Star Trek:
25th Anniversary. Tested on mono.

The game stores its files in compressed binary blobs, so this mainly deals with extracting
and re-compressing them.

## Dumping files

Assuming a unix-ish environment, run the following commands:

```
mkdir <output_dir>
startrek25_rtools.exe --dumpallfiles <trek_dir> <output_dir>
```

Where "trek\_dir" is the directory of the game files (should contain DATA.DIR, etc), and
"output\_dir" is the directory to put the files in.

## Reinserting modified files into the game

Star Trek: 25th Anniversary looks for a "patches" folder from which to load any files.
So, files don't need to be repackaged into the original binary blob. However, files in
this folder must be compressed.

There is a makefile in the "staging" directory. In it, set the "EXE" variable to the
location of the executable. Then, creates a folder named "patches" in the game data folder
(the folder with DATA.DIR) and set "PATCH\_DIR" to the location of that folder.

Then, in the staging folder, create a folder named "files" which contains the uncompressed
versions of the files to be compressed. Run "make", and all files from there will be
compressed to the patch directory. Now, you can run Star Trek, and the files in the
"patches" directory will replace the files in the original binary blobs.

*WARNING*: The memory usage of the recompression is unreasonably high in some cases. The
larger files can use about 1.5G to be compressed. You probably shouldn't run make with
multithreading or that number will multiply.

## Commands

* `--packfiles <uncmp_dir> <cmp_dir> <trek_dir>`

This is experimental, but it's supposed to repackage the DATA.DIR, DATA.001, and DATA.RUN
files given a directory of uncompressed files, and a corresponding directory of compressed
files. Just use the "patches" directory instead...

* `--compressfile <infile> <outfile>`

Compresses a file to be used in the "patches" directory.

* `--decompressfile <infile> <outfile>`

Decompresses a file from the "patches" directory.

* `--dumpallfiles <trek_dir> <output_dir>`

Decompresses all files from DATA.001/DATA.RUN/DATA.DIR into the given folder.

* `--dumpallfilescmp <trek_dir> <output_dir>`

Dumps the compressed versions of all files from DATA.001/DATA.RUN/DATA.DIR into the given
folder.

* `--dumprdftexttable <rdf_file> <address>`

Dumps a table of text from an address in an RDF file to stdout.

* `--dumprdftext <rdf_file> <address>`

Dumps a string from an address in an RDF file to stdout.

* `--dumpscript <trek_dir> <room_name> <sfx_dir>`

Dumps x86 code from an RDF file to a txt file using objdump. Code is sorted by the actions
that call them (ie. look at, talk to). Assumes that a "scripts/" directory exists.

"sfx\_dir" should be the location of the "voc/sfx" directory that comes from the CD. It reads
the filenames to help detect strings and separate them from code.

`room_name` should not have the rdf extension, ie:

`startrek25_rtools.exe --dumpscript TREKCD DEMON0 voc/sfx/`

* `--dumpallscripts <trek_dir> <sfx_dir>`

Same as running `--dumpscript` on all RDF files. Assumes there's a "scripts/" directory to
dump them to.
