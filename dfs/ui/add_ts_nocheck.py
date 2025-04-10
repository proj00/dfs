import os
import sys


def add_ts_nocheck_to_file(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Check if first non-empty line already has @ts-nocheck
    lines = content.splitlines()
    for line in lines:
        if line.strip():  # first non-empty line
            if "@ts-nocheck" in line:
                print(f"Skipping (already has @ts-nocheck): {file_path}")
                return
            break

    with open(file_path, "w", encoding="utf-8") as f:
        f.write("// @ts-nocheck\n" + content)
        print(f"Added @ts-nocheck to: {file_path}")


def process_directory(directory):
    for root, _, files in os.walk(directory):
        for filename in files:
            if filename.endswith((".ts", ".tsx")):
                file_path = os.path.join(root, filename)
                add_ts_nocheck_to_file(file_path)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python add_ts_nocheck.py <directory>")
        sys.exit(1)

    target_dir = sys.argv[1]

    if os.path.isdir(target_dir):
        process_directory(target_dir)
    else:
        print(f"Error: '{target_dir}' is not a valid directory.")
        sys.exit(1)
