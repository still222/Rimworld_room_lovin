import os
import shutil
import msvcrt
import traceback
import re

# Bumping version shenanigans
def bump_version(value: str) -> str:
	# extract first numeric version-like sequence
	start = None

	for i, c in enumerate(value):
		if c.isdigit():
			start = i
			break

	if start is None:
		return value

	version_str = value[start:]
	parts = version_str.split(".")

	# ensure at least MAJOR.MINOR.PATCH
	while len(parts) < 3:
		parts.append("0")

	# bump PATCH always
	parts[-1] = str(int(parts[-1]) + 1)

	new_version = ".".join(parts)

	return value[:start] + new_version

def bump_mod_version(root_path):
	about_path = os.path.join(root_path, "About", "About.xml")

	if not os.path.isfile(about_path):
		print("About.xml not found.")
		return

	with open(about_path, "r", encoding="utf-8") as f:
		content = f.read()

	match = re.search(r"<modVersion>(.*?)</modVersion>", content, re.DOTALL)
	if not match:
		print("modVersion not found.")
		return

	old_version = match.group(1)
	new_version = bump_version(old_version)

	if old_version == new_version:
		print("No change applied.")
		return

	content = (
		content[:match.start()]
		+ f"<modVersion>{new_version}</modVersion>"
		+ content[match.end():]
	)

	with open(about_path, "w", encoding="utf-8") as f:
		f.write(content)

	print(f"Version bumped: {old_version} → {new_version}")

# Function to build dlls
def build_projects(source_path):
	if not os.path.isdir(source_path):
		print("No Source folder found, skipping build.")
		return

	print("\nSearching for C# projects...")

	csproj_files = []

	for root, dirs, files in os.walk(source_path):
		for file in files:
			if file.endswith(".csproj"):
				csproj_files.append(os.path.join(root, file))

	if not csproj_files:
		print("No .csproj files found.")
		return

	for project in csproj_files:
		print(f"\nBuilding: {project}")

		result = os.system(f'dotnet build "{project}"')

		if result != 0:
			raise RuntimeError(f"Build failed:\n{project}")

	print("\nAll projects built successfully.")

# Ignore function for folders and files
def ignore_filter(dir, contents):
	ignored = []

	for item in contents:
		# Ignore Source folder
		if item == "Source":
			ignored.append(item)

		# Ignore .git folder anywhere
		elif item == ".git":
			ignored.append(item)

		# Ignore .gitignore only in root
		elif item == ".gitignore":
			ignored.append(item)

		# Ignore all .py files
		elif item.endswith(".py"):
			ignored.append(item)

	return ignored


def main():
	# Current working directory = mod root
	root_path = os.getcwd()
	root_name = os.path.basename(root_path)
	target_name = f"x{root_name}"

	print(f"Root detected: {root_name}")
	print(f"Path: {root_path}")

	# Find RimWorld root automatically
	search_path = root_path
	rimworld_root = None

	while True:
		current_name = os.path.basename(search_path)

		if (
			current_name == "RimWorld"
			and os.path.isdir(os.path.join(search_path, "Mods"))
		):
			rimworld_root = search_path
			break

		parent = os.path.dirname(search_path)

		# Reached filesystem root
		if parent == search_path:
			break

		search_path = parent

	if rimworld_root is None:
		print("ERROR: Could not locate RimWorld folder!")
		msvcrt.getch()
		return

	target_base = os.path.join(rimworld_root, "Mods")
	target_path = os.path.join(target_base, target_name)

	print(f"Target Mods folder: {target_base}")
	print(f"Final target path: {target_path}")

	# Safety check
	if not os.path.isdir(target_base):
		print("ERROR: Mods directory does not exist!")
		msvcrt.getch()
		return

	print("Press any key to continue...")
	msvcrt.getch()

	if os.path.exists(target_path):
		print("Target exists, removing...")
		shutil.rmtree(target_path)

	source_path = os.path.join(root_path, "Source")
	build_projects(source_path)

	print("Copying files...")
	shutil.copytree(
		root_path,
		target_path,
		ignore=ignore_filter
	)

	bump_mod_version(root_path)

	print("Done!")

if __name__ == "__main__":
	try:
		main()
	except Exception:
		traceback.print_exc()
		print("\nPress Enter to exit...")
		msvcrt.getch()