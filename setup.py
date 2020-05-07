from setuptools import setup
from codecs import open
from os import path

here = path.abspath(path.dirname(__file__))

with open(path.join(here, 'README.md')) as f:
    long_description = ''.join(f.readlines())

# get the dependencies and installs
with open(path.join(here, 'requirements.txt'), encoding='utf-8') as f:
    all_reqs = f.read().split('\n')

install_requires = [x.strip() for x in all_reqs if 'git+' not in x]
dependency_links = [x.strip().replace('git+', '') for x in all_reqs if x.startswith('git+')]

setup(name='simview',
      version='0.1',
      description='3D visualization for molecular simulations',
      long_description=long_description,
      url='https://github.com/ur-whitelab/simview',
      author='UR Whitelab',
      packages=['simview'],
      install_requires=install_requires,
      dependency_links=dependency_links,
      author_email='andrew.white@rochester.edu',
      entry_points=
       {
         'console_scripts': [
           'broker=simview.broker:main',
           'smiles_sim=simview.smiles_sim:main'
          ],
       }
     )
