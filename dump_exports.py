import ctypes
import pefile
import sys

dll_path = sys.argv[1]
pe = pefile.PE(dll_path)
if hasattr(pe, 'DIRECTORY_ENTRY_EXPORT'):
    for exp in pe.DIRECTORY_ENTRY_EXPORT.symbols:
        print(exp.name.decode('utf-8') if exp.name else 'Ordinal: ' + str(exp.ordinal))
else:
    print("No exports found.")
