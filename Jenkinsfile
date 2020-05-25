pipeline {
  agent any
  stages {
    stage('Dependencies') {
      steps {
        sh 'nuget restore SCPermissions.sln'
      }
    }
    stage('Build') {
      steps {
        sh 'msbuild SCPermissions/SCPermissions.csproj -restore -p:PostBuildEvent='
      }
    }
    stage('Setup Output Dir') {
      steps {
        sh 'mkdir Plugin'
        sh 'mkdir Plugin/dependencies'
      }
    }
    stage('Package') {
      steps {
        sh 'mv SCPermissions/bin/SCPermissions.dll Plugin/'
        sh 'mv SCPermissions/bin/YamlDotNet.dll Plugin/dependencies'
        sh 'mv SCPermissions/bin/Newtonsoft.Json.dll Plugin/dependencies'
      }
    }
    stage('Archive') {
      steps {
        sh 'zip -r SCPermissions.zip Plugin'
        archiveArtifacts(artifacts: 'SCPermissions.zip', onlyIfSuccessful: true)
      }
    }
  }
}
