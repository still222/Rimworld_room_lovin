import os
import shutil
import msvcrt

def main():
	# Current working directory = mod root
	root_path = os.getcwd()
	root_name = os.path.basename(root_path)
	target_name = f"x{root_name}"

	print(f"Root detected: {root_name}")
	print(f"Path: {root_path}")

	# Target path: ../../../Mods/<root_name>
	target_base = os.path.abspath(os.path.join(root_path, "..", "..", "..", "Mods"))
	target_path = os.path.join(target_base, target_name)

	print(f"Target Mods folder: {target_base}")
	print(f"Final target path: {target_path}")

	print("Press any key to continue...")
	msvcrt.getch()

	# Safety check
	if not os.path.isdir(target_base):
		print("ERROR: Mods directory does not exist!")
		msvcrt.getch()
		return

	# Check if target exists
	if os.path.exists(target_path):
		while True:
			choice = input(f"Mod '{target_name}' already exists. Overwrite? (y/n): ").strip().lower()
			if choice in ("y", "yes"):
				print("Removing existing mod folder...")
				shutil.rmtree(target_path)
				break
			elif choice in ("n", "no"):
				print("Operation cancelled.")
				return
			else:
				print("Please enter 'y' or 'n'.")

	# Ignore function
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

	print("Copying files...")
	shutil.copytree(
		root_path,
		target_path,
		ignore=ignore_filter
	)

	print("Done!")
	msvcrt.getch()

if __name__ == "__main__":
	main()