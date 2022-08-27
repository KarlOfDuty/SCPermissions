pipeline {
  agent any
  stages {
    stage('Dependencies') {
      steps {
        sh 'nuget restore SCPermissions.sln'
      }
    }
    stage('Switch Smod version') {
        when { triggeredBy 'BuildUpstreamCause' }
        steps {
            sh ('rm SCPermissions/lib/Assembly-CSharp.dll')
            sh ('rm SCPermissions/lib/Smod2.dll')
            sh ('ln -s $SCPSL_LIBS/Assembly-CSharp.dll SCPermissions/lib/Assembly-CSharp.dll')
            sh ('ln -s $SCPSL_LIBS/Smod2.dll SCPermissions/lib/Smod2.dll')
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
        when { not { triggeredBy 'BuildUpstreamCause' } }
        steps {
            sh 'zip -r SCPermissions.zip SCPermissions/*'
            archiveArtifacts(artifacts: 'SCPermissions.zip', onlyIfSuccessful: true)
        }
    }
    stage('Send upstream') {
        when { triggeredBy 'BuildUpstreamCause' }
        steps {
            sh 'zip -r SCPermissions.zip SCPermissions/*'
            sh 'cp SCPermissions.zip $PLUGIN_BUILDER_ARTIFACT_DIR'
        }
    }
  }
}
