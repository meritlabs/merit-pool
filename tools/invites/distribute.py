import argparse
import subprocess
import config

# Parse arguments
ap = argparse.ArgumentParser()
ap.add_argument("-f", "--filename", required=False, help="Filename(from where read the distribution data)",
                default="mempool_invites.csv")
args = vars(ap.parse_args())


def distribute_invites():
    with open(args["filename"], "r") as f:
        for i in f.readlines():
            entry = i.strip().split(",")

            command = [
                'merit-cli',
                '-conf={}'.format(config.MERIT_CONF),
                '-datadir={}'.format(config.MERIT_DATA_DIR),
                'inviteaddress',
                '"{}"'.format(entry[0]),
                '{}'.format(entry[1])
            ]

            print("COMMAND: ", " ".join(command))
            print(subprocess.check_output(command))


if __name__ == "__main__":
    distribute_invites()
